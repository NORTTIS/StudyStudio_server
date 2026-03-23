using Moq;
using Microsoft.Extensions.Logging;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;
using Xunit;

namespace StudioStudio_Server.Tests.Services;

public class AIServiceTests
{
    private readonly Mock<IGroupParticipantRepository> _participantRepo;
    private readonly Mock<IEmbeddingService> _embeddingService;
    private readonly Mock<IVectorDatabaseService> _vectorDbService;
    private readonly Mock<ITaskRepository> _taskRepo;
    private readonly Mock<ILLMService> _llmService;
    private readonly Mock<IUserSubscriptionRepository> _subscriptionRepo;
    private readonly Mock<IAIRequestLogRepository> _aiRequestLogRepo;
    private readonly Mock<ILogger<AIService>> _logger;
    private readonly AIService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public AIServiceTests()
    {
        _participantRepo = new Mock<IGroupParticipantRepository>();
        _embeddingService = new Mock<IEmbeddingService>();
        _vectorDbService = new Mock<IVectorDatabaseService>();
        _taskRepo = new Mock<ITaskRepository>();
        _llmService = new Mock<ILLMService>();
        _subscriptionRepo = new Mock<IUserSubscriptionRepository>();
        _aiRequestLogRepo = new Mock<IAIRequestLogRepository>();
        _logger = new Mock<ILogger<AIService>>();

        _sut = new AIService(
            _participantRepo.Object,
            _embeddingService.Object,
            _vectorDbService.Object,
            _taskRepo.Object,
            _llmService.Object,
            _subscriptionRepo.Object,
            _aiRequestLogRepo.Object,
            _logger.Object);
    }

    private static float[] FakeEmbedding() => Enumerable.Range(0, 768).Select(_ => 0.1f).ToArray();

    private static List<VectorSearchResponse.SearchResult> FakeSearchResults(int count)
    {
        return Enumerable.Range(0, count).Select(i => new VectorSearchResponse.SearchResult
        {
            Id = $"chunk_{i}",
            Score = 0.9f - (i * 0.05f),
            Payload = new Dictionary<string, object>
            {
                ["documentId"] = Guid.NewGuid().ToString(),
                ["fileName"] = $"doc_{i}.pdf",
                ["content"] = $"Document content {i}",
                ["chunkIndex"] = i
            }
        }).ToList();
    }

    #region Rate Limiting

    [Fact]
    public async Task AskQuestionAsync_WithinRateLimit_ReturnsSuccessResponse()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "What is AI?" };
        var embedding = FakeEmbedding();
        var searchResults = FakeSearchResults(2);
        var taskSummary = new TaskSummaryResponse { TotalTasks = 10, CompletedTasks = 5 };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(request.Question, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _vectorDbService.Setup(x => x.SearchVectorsAsync(embedding, 3, _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(taskSummary);
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), request.Question, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("AI is artificial intelligence.");

        // Act
        var result = await _sut.AskQuestionAsync(_userId, request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("AI is artificial intelligence.", result.Answer);
        Assert.Equal(2, result.SourceDocuments.Count);
        Assert.Equal(14, result.RemainingRequests); // 20 - (5+1)
        Assert.Equal(20, result.DailyLimit);
    }

    [Fact]
    public async Task AskQuestionAsync_AtRateLimit_ThrowsAppException()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(20);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(
            () => _sut.AskQuestionAsync(_userId, request));

        Assert.Equal(ErrorCodes.AIRateLimitExceeded, ex.Code);
    }

    [Fact]
    public async Task AskQuestionAsync_NoSubscription_DefaultsToFreePlan()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(15); // 15 < 20 (free plan default)
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync((SubscriptionPlan?)null); // No subscription
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), 3, _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResults(0));
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(new TaskSummaryResponse());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        var result = await _sut.AskQuestionAsync(_userId, request);

        // Assert
        Assert.Equal(20, result.DailyLimit); // Free plan default
        Assert.Equal(4, result.RemainingRequests); // 20 - (15+1)
    }

    #endregion

    #region Permission

    [Fact]
    public async Task AskQuestionAsync_UserNotMember_ThrowsForbidden()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(false); // Not a member

        // Act & Assert
        var ex = await Assert.ThrowsAsync<AppException>(
            () => _sut.AskQuestionAsync(_userId, request));

        Assert.Equal(ErrorCodes.GroupPermissionDenied, ex.Code);
    }

    #endregion

    #region Document Search

    [Fact]
    public async Task AskQuestionAsync_QdrantReturnsEmpty_StillCallsLLM()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>()); // Empty
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(new TaskSummaryResponse());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("No documents found.");

        // Act
        var result = await _sut.AskQuestionAsync(_userId, request);

        // Assert
        Assert.Equal("No documents found.", result.Answer);
        Assert.Empty(result.SourceDocuments);
        _llmService.Verify(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), request.Question, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AskQuestionAsync_MultipleChunks_ReturnsTop3()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResults(5)); // 5 results, service passes topK=3 to Qdrant
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(new TaskSummaryResponse());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        // Act
        var result = await _sut.AskQuestionAsync(_userId, request);

        // Assert — verify service called Qdrant with topK=3 (returns whatever Qdrant gives)
        _vectorDbService.Verify(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), 3, _groupId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Task Summary

    [Fact]
    public async Task AskQuestionAsync_TaskSummaryPassedToLLMContext()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };
        var taskSummary = new TaskSummaryResponse
        {
            TotalTasks = 10,
            CompletedTasks = 2,
            OverdueTasks = 3,
            CompletionPercentage = 20
        };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), 3, _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>());
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(taskSummary);

        string capturedContext = "";
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, ctx, _) => capturedContext = ctx)
            .ReturnsAsync("Answer");

        // Act
        await _sut.AskQuestionAsync(_userId, request);

        // Assert
        Assert.Contains("THỐNG KÊ CÔNG VIỆC", capturedContext);
        Assert.Contains("Tổng số tasks:", capturedContext);
        Assert.Contains("Đã hoàn thành:", capturedContext);
    }

    #endregion

    #region Request Logging

    [Fact]
    public async Task AskQuestionAsync_Success_LogsAIRequest()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), 3, _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>());
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(new TaskSummaryResponse());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Answer");

        AIRequestLog? capturedLog = null;
        _aiRequestLogRepo.Setup(x => x.AddAsync(It.IsAny<AIRequestLog>()))
            .Callback<AIRequestLog>(log => capturedLog = log)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AskQuestionAsync(_userId, request);

        // Assert
        _aiRequestLogRepo.Verify(x => x.AddAsync(It.IsAny<AIRequestLog>()), Times.Once);
        Assert.NotNull(capturedLog);
        Assert.Equal(_userId, capturedLog.UserId);
        Assert.True(capturedLog.TokenUsed > 0);
    }

    [Fact]
    public async Task AskQuestionAsync_Streaming_ReturnsMetadataWithRemainingRequests()
    {
        // Arrange
        var request = new AIQuestionRequest { GroupId = _groupId, Question = "?" };

        _aiRequestLogRepo.Setup(x => x.CountTodayRequestsAsync(_userId, It.IsAny<DateTime>()))
            .ReturnsAsync(5);
        _subscriptionRepo.Setup(x => x.GetSubscriptionPlanByUserIdAsync(_userId))
            .ReturnsAsync(new SubscriptionPlan { MaxAiRequestsPerDay = 20 });
        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _vectorDbService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), 3, _groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResults(1));
        _taskRepo.Setup(x => x.GetGroupTaskStatisticsAsync(_groupId))
            .ReturnsAsync(new TaskSummaryResponse { TotalTasks = 5, CompletedTasks = 3 });

        // Act
        var (metadata, _) = await _sut.AskQuestionStreamAsync(_userId, request);

        // Assert
        Assert.Equal(14, metadata.RemainingRequests); // 20 - (5+1)
        Assert.Equal(20, metadata.DailyLimit);
        Assert.Single(metadata.SourceDocuments);
    }

    #endregion
}
