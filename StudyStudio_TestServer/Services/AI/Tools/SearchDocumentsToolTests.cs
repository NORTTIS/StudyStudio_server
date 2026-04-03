using Moq;
using Microsoft.Extensions.Logging;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json.Nodes;
using Xunit;

namespace StudioStudio_Server.Tests.Services.AI.Tools;

public class SearchDocumentsToolTests
{
    private readonly Mock<IVectorDatabaseService> _qdrantService;
    private readonly Mock<IEmbeddingService> _embeddingService;
    private readonly Mock<IGroupParticipantRepository> _participantRepo;
    private readonly Mock<ILogger<SearchDocumentsTool>> _logger;
    private readonly SearchDocumentsTool _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();

    public SearchDocumentsToolTests()
    {
        _qdrantService = new Mock<IVectorDatabaseService>();
        _embeddingService = new Mock<IEmbeddingService>();
        _participantRepo = new Mock<IGroupParticipantRepository>();
        _logger = new Mock<ILogger<SearchDocumentsTool>>();

        _sut = new SearchDocumentsTool(
            _qdrantService.Object,
            _embeddingService.Object,
            _participantRepo.Object,
            _logger.Object);
    }

    private static float[] FakeEmbedding() => Enumerable.Range(0, 768).Select(_ => 0.1f).ToArray();

    private static List<VectorSearchResponse.SearchResult> FakeSearchResults(int count)
    {
        return Enumerable.Range(0, count).Select(i => new VectorSearchResponse.SearchResult
        {
            Id = $"doc_{i}_0",
            Score = 0.95f - (i * 0.05f),
            Payload = new Dictionary<string, object>
            {
                ["documentId"] = Guid.NewGuid().ToString(),
                ["fileName"] = $"lecture_{i}.pdf",
                ["content"] = $"Content about topic {i}",
                ["chunkIndex"] = 0
            }
        }).ToList();
    }

    #region ValidateParameters

    [Fact]
    public void ValidateParameters_ValidQueryAndGroupId_ReturnsTrue()
    {
        var p = new JsonObject
        {
            ["query"] = "machine learning",
            ["group_id"] = _groupId.ToString()
        };
        Assert.True(_sut.ValidateParameters(p));
    }

    [Fact]
    public void ValidateParameters_EmptyQuery_ReturnsFalse()
    {
        var p = new JsonObject
        {
            ["query"] = "   ",
            ["group_id"] = _groupId.ToString()
        };
        Assert.False(_sut.ValidateParameters(p));
    }

    [Fact]
    public void ValidateParameters_InvalidGroupId_ReturnsTrue()
    {
        var p = new JsonObject
        {
            ["query"] = "test query",
            ["group_id"] = "not-a-guid"
        };
        Assert.True(_sut.ValidateParameters(p));
    }

    [Fact]
    public void ValidateParameters_MissingGroupId_ReturnsTrue()
    {
        var p = new JsonObject { ["query"] = "test" };
        Assert.True(_sut.ValidateParameters(p));
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_ValidParams_ReturnsDocumentsWithScores()
    {
        // Arrange
        var query = "What is AI?";
        var embedding = FakeEmbedding();
        var searchResults = FakeSearchResults(2);
        var parameters = new JsonObject
        {
            ["query"] = query,
            ["group_id"] = _groupId.ToString(),
            ["top_k"] = 3
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);
        _qdrantService.Setup(x => x.SearchVectorsAsync(It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data["total_found"]!.GetValue<int>());
        Assert.NotNull(result.Data["documents"]);
        Assert.NotNull(result.Data["summary"]);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotMember_ReturnsError()
    {
        // Arrange
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["group_id"] = _groupId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Ban khong co quyen truy cap nhom nay", result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_QdrantReturnsEmpty_ReturnsEmptyDocuments()
    {
        // Arrange
        var parameters = new JsonObject
        {
            ["query"] = "nonexistent topic",
            ["group_id"] = _groupId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>());

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!["total_found"]!.GetValue<int>());
        Assert.Empty((JsonArray)result.Data["documents"]!);
    }

    [Fact]
    public async Task ExecuteAsync_EnglishLanguage_ReturnsEnglishSummary()
    {
        // Arrange
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["group_id"] = _groupId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "en" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResults(1));

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.Contains("Found", result.Data!["summary"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExecuteAsync_DefaultTopK_Uses3()
    {
        // Arrange
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["group_id"] = _groupId.ToString()
            // no top_k
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResults(0));

        // Act
        await _sut.ExecuteAsync(context, parameters);

        // Assert
        _qdrantService.Verify(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), 3, _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_IncludesRelevanceScore()
    {
        // Arrange
        var searchResults = FakeSearchResults(1);
        searchResults[0].Score = 0.8765f;

        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["group_id"] = _groupId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        _participantRepo.Setup(x => x.IsUserInGroupAsync(_groupId, _userId))
            .ReturnsAsync(true);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), _groupId, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        var doc = (JsonObject)((JsonArray)result.Data!["documents"]!)[0]!;
        Assert.Equal(0.8765, doc["relevance_score"]!.GetValue<double>(), 4);
    }

    #endregion
}
