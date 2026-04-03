using Moq;
using Microsoft.Extensions.Logging;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json.Nodes;
using Xunit;

namespace StudioStudio_Server.Tests.Services.AI.Tools;

public class SearchStudioDocumentsToolTests
{
    private readonly Mock<IVectorDatabaseService> _qdrantService;
    private readonly Mock<IEmbeddingService> _embeddingService;
    private readonly Mock<IStudioRepository> _studioRepo;
    private readonly Mock<ILogger<SearchStudioDocumentsTool>> _logger;
    private readonly SearchStudioDocumentsTool _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _studioId = Guid.NewGuid();
    private readonly Guid _groupId1 = Guid.NewGuid();
    private readonly Guid _groupId2 = Guid.NewGuid();

    public SearchStudioDocumentsToolTests()
    {
        _qdrantService = new Mock<IVectorDatabaseService>();
        _embeddingService = new Mock<IEmbeddingService>();
        _studioRepo = new Mock<IStudioRepository>();
        _logger = new Mock<ILogger<SearchStudioDocumentsTool>>();

        _sut = new SearchStudioDocumentsTool(
            _qdrantService.Object,
            _embeddingService.Object,
            _studioRepo.Object,
            _logger.Object);
    }

    private static float[] FakeEmbedding() => Enumerable.Range(0, 768).Select(_ => 0.1f).ToArray();

    private static List<VectorSearchResponse.SearchResult> FakeSearchResultsFromGroups(List<Group> groups)
    {
        var results = new List<VectorSearchResponse.SearchResult>();
        foreach (var group in groups)
        {
            results.Add(new VectorSearchResponse.SearchResult
            {
                Id = $"doc_{group.GroupId}_0",
                Score = 0.9f,
                Payload = new Dictionary<string, object>
                {
                    ["documentId"] = Guid.NewGuid().ToString(),
                    ["fileName"] = $"doc_for_{group.GroupName}.pdf",
                    ["content"] = $"Content for {group.GroupName}",
                    ["chunkIndex"] = 0,
                    ["groupId"] = group.GroupId.ToString()
                }
            });
        }
        return results;
    }

    #region ValidateParameters

    [Fact]
    public void ValidateParameters_ValidQueryAndStudioId_ReturnsTrue()
    {
        var p = new JsonObject
        {
            ["query"] = "machine learning",
            ["studio_id"] = _studioId.ToString()
        };
        Assert.True(_sut.ValidateParameters(p));
    }

    [Fact]
    public void ValidateParameters_EmptyQuery_ReturnsFalse()
    {
        var p = new JsonObject
        {
            ["query"] = "",
            ["studio_id"] = _studioId.ToString()
        };
        Assert.False(_sut.ValidateParameters(p));
    }

    [Fact]
    public void ValidateParameters_InvalidStudioId_ReturnsFalse()
    {
        var p = new JsonObject
        {
            ["query"] = "test",
            ["studio_id"] = "invalid-guid"
        };
        Assert.False(_sut.ValidateParameters(p));
    }

    #endregion

    #region ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_ValidParams_ReturnsDocumentsFromAllGroups()
    {
        // Arrange
        var groups = new List<Group>
        {
            new() { GroupId = _groupId1, GroupName = "Group Alpha" },
            new() { GroupId = _groupId2, GroupName = "Group Beta" }
        };
        var searchResults = FakeSearchResultsFromGroups(groups);
        var parameters = new JsonObject
        {
            ["query"] = "machine learning",
            ["studio_id"] = _studioId.ToString(),
            ["top_k"] = 5
        };
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(groups);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync("machine learning", It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), 5, It.Is<List<Guid>>(g => g.Count == 2), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data["total_found"]!.GetValue<int>());
        Assert.Equal(2, result.Data["groups_searched"]!.GetValue<int>());
    }

    [Fact]
    public async Task ExecuteAsync_StudioWithNoGroups_ReturnsEmptyDocuments()
    {
        // Arrange
        var parameters = new JsonObject
        {
            ["query"] = "any topic",
            ["studio_id"] = _studioId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(new List<Group>());

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data!["total_found"]!.GetValue<int>());
        Assert.Equal(0, result.Data["groups_searched"]!.GetValue<int>());
        Assert.Empty((JsonArray)result.Data["documents"]!);
    }

    [Fact]
    public async Task ExecuteAsync_ResultsIncludeGroupName()
    {
        // Arrange
        var groups = new List<Group>
        {
            new() { GroupId = _groupId1, GroupName = "Math Class" },
            new() { GroupId = _groupId2, GroupName = "Physics Class" }
        };
        var searchResults = FakeSearchResultsFromGroups(groups);
        var parameters = new JsonObject
        {
            ["query"] = "calculus",
            ["studio_id"] = _studioId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(groups);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<List<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResults);

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        var doc = (JsonObject)((JsonArray)result.Data!["documents"]!)[0]!;
        Assert.NotNull(doc["group_name"]);
        Assert.NotNull(doc["group_id"]);
    }

    [Fact]
    public async Task ExecuteAsync_QdrantMultiGroupSearch_CalledWithAllGroupIds()
    {
        // Arrange
        var groups = new List<Group>
        {
            new() { GroupId = _groupId1, GroupName = "Group 1" },
            new() { GroupId = _groupId2, GroupName = "Group 2" }
        };
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["studio_id"] = _studioId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(groups);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<List<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>());

        // Act
        await _sut.ExecuteAsync(context, parameters);

        // Assert
        _qdrantService.Verify(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(),
            It.IsAny<int>(),
            It.Is<List<Guid>>(ids => ids.Contains(_groupId1) && ids.Contains(_groupId2)),
            It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EnglishLanguage_ReturnsEnglishSummary()
    {
        // Arrange
        var groups = new List<Group>
        {
            new() { GroupId = _groupId1, GroupName = "Group" }
        };
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["studio_id"] = _studioId.ToString()
        };
        var context = new AIQueryContext { UserId = _userId, Language = "en" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(groups);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<List<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeSearchResultsFromGroups(groups));

        // Act
        var result = await _sut.ExecuteAsync(context, parameters);

        // Assert
        var summary = result.Data!["summary"]!.GetValue<string>();
        Assert.Contains("Found", summary);
        Assert.Contains("group", summary);
    }

    [Fact]
    public async Task ExecuteAsync_DefaultTopK_Uses5()
    {
        // Arrange
        var groups = new List<Group>
        {
            new() { GroupId = _groupId1, GroupName = "Group" }
        };
        var parameters = new JsonObject
        {
            ["query"] = "test",
            ["studio_id"] = _studioId.ToString()
            // no top_k
        };
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _studioRepo.Setup(x => x.GetGroupsByStudioIdAsync(_studioId))
            .ReturnsAsync(groups);
        _embeddingService.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(FakeEmbedding());
        _qdrantService.Setup(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), 5, It.IsAny<List<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorSearchResponse.SearchResult>());

        // Act
        await _sut.ExecuteAsync(context, parameters);

        // Assert
        _qdrantService.Verify(x => x.SearchVectorsMultiGroupAsync(
            It.IsAny<float[]>(), 5, It.IsAny<List<Guid>>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
