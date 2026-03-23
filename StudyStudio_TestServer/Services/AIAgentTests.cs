using Moq;
using Microsoft.Extensions.Logging;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace StudioStudio_Server.Tests.Services;

public class AIAgentTests
{
    private readonly Mock<IAIToolRegistry> _toolRegistry;
    private readonly Mock<ILLMService> _llmService;
    private readonly Mock<ILogger<AIAgent>> _logger;
    private readonly AIAgent _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _studioId = Guid.NewGuid();

    public AIAgentTests()
    {
        _toolRegistry = new Mock<IAIToolRegistry>();
        _llmService = new Mock<ILLMService>();
        _logger = new Mock<ILogger<AIAgent>>();
        var serviceProvider = new Mock<IServiceProvider>();
        _sut = new AIAgent(_toolRegistry.Object, serviceProvider.Object, _llmService.Object, _logger.Object);
    }

    private static JsonObject EmptyToolManifest() => new JsonObject
    {
        ["tools"] = new JsonArray()
    };

    #region Direct Answer

    [Fact]
    public async Task ProcessAsync_LLMReturnsDirectAnswer_ReturnsAnswerWithoutToolCalls()
    {
        // Arrange
        var question = "What is the capital of France?";
        var context = new AIQueryContext { UserId = _userId, Language = "en" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), question, "", It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"action":"answer","final_answer":"Paris is the capital of France."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Paris is the capital of France.", result.Answer);
        Assert.Equal(0, result.ToolCallCount);
        Assert.Empty(result.ToolCalls);
    }

    [Fact]
    public async Task ProcessAsync_LLMReturnsNonJsonText_TreatedAsDirectAnswer()
    {
        // Arrange
        var question = "Hello";
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), question, "", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Xin chao! Ban can giup gi?");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Xin chao", result.Answer);
    }

    #endregion

    #region Tool Call Execution

    [Fact]
    public async Task ProcessAsync_LLMReturnsToolCall_ExecutesToolAndResponds()
    {
        // Arrange
        var question = "Show me group tasks";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray()
            }, 50));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_tasks")).Returns(mockTool.Object);

        // First call: LLM decides to call tool
        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), question, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            // Second call: LLM returns final answer after tool result
            .ReturnsAsync("""{"action":"answer","final_answer":"Ban co 5 cong viec trong nhom."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ToolCallCount);
        Assert.Contains("cong viec", result.Answer);
        mockTool.Verify(x => x.ExecuteAsync(
            It.Is<AIQueryContext>(c => c.UserId == _userId && c.GroupId == _groupId),
            It.IsAny<JsonObject>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_ToolReturnsError_LogsErrorAndContinues()
    {
        // Arrange
        var question = "Show members";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_members");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Error("Ban khong co quyen truy cap nhom nay"));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_members")).Returns(mockTool.Object);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_members"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        // Note: When tool returns error, loop breaks but FinalAnswer stays null (known behavior)
        Assert.True(result.Success);
        Assert.Equal(1, result.ToolCallCount);
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Loop Limits

    [Fact]
    public async Task ProcessAsync_MaxToolCallsExceeded_StopsAt5Calls()
    {
        // Arrange
        var question = "Complex multi-tool query";
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject(), 10));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_tasks")).Returns(mockTool.Object);

        // LLM always returns tool_call (infinite loop simulation)
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.Equal(5, result.ToolCallCount); // Stops at max
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Exactly(5));
    }

    [Fact]
    public async Task ProcessAsync_ToolFailsDuringLoop_BreaksLoop()
    {
        // Arrange
        var question = "Show stats";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_group_stats");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Error("Task failed"));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_group_stats")).Returns(mockTool.Object);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_group_stats"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.Equal(1, result.ToolCallCount);
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Invalid Tool / Parameters

    [Fact]
    public async Task ProcessAsync_ToolNotFound_ReturnsErrorMessage()
    {
        // Arrange
        var question = "Use unknown tool";
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("nonexistent")).Returns((IAITool?)null);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"action":"tool_call","tool_name":"nonexistent","parameters":{}}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ToolCallCount);
        Assert.Equal(3, result.ReasoningSteps.Count);
    }

    [Fact]
    public async Task ProcessAsync_InvalidParameters_LogsWarningAndContinues()
    {
        // Arrange
        var question = "Get tasks with bad params";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(false); // Invalid!

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_tasks")).Returns(mockTool.Object);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ToolCallCount);
        // Tool should NOT be executed because ValidateParameters returned false
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Language Context

    [Fact]
    public async Task ProcessAsync_VietnameseLanguage_UsesVietnameseSystemPrompt()
    {
        // Arrange
        var question = "Cho hoi ve nhom";
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());

        string capturedPrompt = "";
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((sysPrompt, _, _, _) => capturedPrompt = sysPrompt)
            .ReturnsAsync("""{"action":"answer","final_answer":"OK"}""");

        // Act
        await _sut.ProcessAsync(question, context);

        // Assert
        Assert.Contains("Study Studio", capturedPrompt);
        Assert.Contains("Bạn là trợ lý AI", capturedPrompt);
    }

    [Fact]
    public async Task ProcessAsync_EnglishLanguage_UsesEnglishSystemPrompt()
    {
        // Arrange
        var question = "Tell me about my group";
        var context = new AIQueryContext { UserId = _userId, Language = "en" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());

        string capturedPrompt = "";
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((sysPrompt, _, _, _) => capturedPrompt = sysPrompt)
            .ReturnsAsync("""{"action":"answer","final_answer":"OK"}""");

        // Act
        await _sut.ProcessAsync(question, context);

        // Assert
        Assert.Contains("Study Studio", capturedPrompt);
        Assert.Contains("You are an AI assistant", capturedPrompt);
    }

    #endregion

    #region Context Fields

    [Fact]
    public async Task ProcessAsync_GroupIdContext_PassesGroupIdToTools()
    {
        // Arrange
        var question = "Group tasks";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject()));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_tasks")).Returns(mockTool.Object);

        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"Done"}""");

        // Act
        await _sut.ProcessAsync(question, context);

        // Assert
        mockTool.Verify(x => x.ExecuteAsync(
            It.Is<AIQueryContext>(c => c.GroupId == _groupId),
            It.IsAny<JsonObject>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_StudioIdContext_PassesStudioIdToTools()
    {
        // Arrange
        var question = "Studio stats";
        var context = new AIQueryContext { UserId = _userId, StudioId = _studioId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_studio_stats");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject()));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_studio_stats")).Returns(mockTool.Object);

        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_studio_stats"",""parameters"":{{""studio_id"":""{_studioId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"OK"}""");

        // Act
        await _sut.ProcessAsync(question, context);

        // Assert
        mockTool.Verify(x => x.ExecuteAsync(
            It.Is<AIQueryContext>(c => c.StudioId == _studioId),
            It.IsAny<JsonObject>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Exception Handling

    [Fact]
    public async Task ProcessAsync_LLMThrowsException_ReturnsErrorResult()
    {
        // Arrange
        var question = "Test exception";
        var context = new AIQueryContext { UserId = _userId, Language = "vi" };

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool>());
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal(0, result.ToolCallCount);
    }

    [Fact]
    public async Task ProcessAsync_ToolThrowsException_LogsErrorAndContinues()
    {
        // Arrange
        var question = "Tool exception test";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        _toolRegistry.Setup(x => x.GetAllTools()).Returns(new List<IAITool> { mockTool.Object });
        _toolRegistry.Setup(x => x.GetToolsManifest()).Returns(EmptyToolManifest());
        _toolRegistry.Setup(x => x.GetTool("get_tasks")).Returns(mockTool.Object);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(1, result.ToolCallCount);
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
