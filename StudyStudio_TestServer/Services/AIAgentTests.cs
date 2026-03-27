using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<IServiceScopeFactory> _scopeFactory;
    private readonly Mock<IServiceScope> _scope;
    private readonly Mock<IServiceProvider> _scopeServiceProvider;
    private readonly AIAgent _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _groupId = Guid.NewGuid();
    private readonly Guid _studioId = Guid.NewGuid();

    public AIAgentTests()
    {
        _toolRegistry = new Mock<IAIToolRegistry>();
        _llmService = new Mock<ILLMService>();
        _logger = new Mock<ILogger<AIAgent>>();

        // Setup IServiceScope chain for CreateScope() extension method
        _scopeServiceProvider = new Mock<IServiceProvider>();
        _scope = new Mock<IServiceScope>();
        _scopeFactory = new Mock<IServiceScopeFactory>();
        _scope.Setup(x => x.ServiceProvider).Returns(_scopeServiceProvider.Object);
        _scopeFactory.Setup(x => x.CreateScope()).Returns(_scope.Object);

        // IServiceProvider -> returns IServiceScopeFactory (used by CreateScope extension)
        _serviceProvider = new Mock<IServiceProvider>().Object;
        Mock.Get(_serviceProvider)
            .Setup(x => x.GetService(typeof(IServiceScopeFactory)))
            .Returns(_scopeFactory.Object);

        _sut = new AIAgent(_toolRegistry.Object, _serviceProvider, _llmService.Object, _logger.Object);

        // Default: GetToolsManifestForContext returns empty manifest (no tools available)
        _toolRegistry.Setup(x => x.GetToolsManifestForContext(It.IsAny<AIQueryContext>()))
            .Returns(new JsonObject { ["tools"] = new JsonArray() });
    }

    /// <summary>
    /// Helper to setup a mock tool so AIAgent can resolve and execute it.
    /// AIAgent uses: GetToolsManifestForContext() -> tools manifest
    ///               GetAllowedTools() -> filter tools by context
    ///               GetToolType() -> resolve type
    ///               IServiceProvider.GetService(IServiceScopeFactory) -> CreateScope() extension
    ///               IServiceScope.ServiceProvider.GetService(toolType) -> resolve tool
    /// </summary>
    private void SetupToolForExecution(Mock<IAITool> mockTool)
    {
        var toolName = mockTool.Object.Name;

        // AIAgent checks if tool is allowed in context
        _toolRegistry.Setup(x => x.GetAllowedTools(It.IsAny<AIQueryContext>()))
            .Returns(new List<IAITool> { mockTool.Object });

        // AIAgent uses GetToolType to get the Type, then resolves via IServiceProvider
        _toolRegistry.Setup(x => x.GetToolType(toolName))
            .Returns(mockTool.Object.GetType());

        // Setup manifest to include this tool
        _toolRegistry.Setup(x => x.GetToolsManifestForContext(It.IsAny<AIQueryContext>()))
            .Returns(new JsonObject
            {
                ["tools"] = new JsonArray(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = toolName,
                        ["description"] = mockTool.Object.Description,
                        ["parameters"] = mockTool.Object.ParametersSchema
                    }
                })
            });

        // AIAgent resolves tool via IServiceScope.ServiceProvider.GetService(toolType)
        _scopeServiceProvider.Setup(x => x.GetService(mockTool.Object.GetType()))
            .Returns(mockTool.Object);
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
        mockTool.Setup(x => x.Description).Returns("Get tasks");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject
            {
                ["tasks"] = new JsonArray()
            }, 50));

        // Setup tool for AIAgent execution
        SetupToolForExecution(mockTool);

        // LLM: tool_call -> (tool result) -> answer (after seeing success result)
        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), question, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"Ban co 5 cong viec trong nhom."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
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
        mockTool.Setup(x => x.Description).Returns("Get members");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Error("Ban khong co quyen truy cap nhom nay"));

        SetupToolForExecution(mockTool);

        // LLM: tool_call -> (tool error) -> answer (after seeing error)
        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_members"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"Da xay ra loi khi lay danh sach thanh vien."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
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
        mockTool.Setup(x => x.Description).Returns("Get tasks");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject(), 10));

        SetupToolForExecution(mockTool);

        // LLM always returns tool_call (infinite loop simulation)
        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.Equal(5, result.ToolCallCount); // Stops at max (no auto-fetch since no GroupId)
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
        mockTool.Setup(x => x.Description).Returns("Get group stats");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Error("Task failed"));

        SetupToolForExecution(mockTool);

        // LLM: tool_call -> (tool error) -> answer (stops retrying after seeing error)
        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_group_stats"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"Khong the lay duoc thong ke nhom."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
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

        // Allow the tool name so agent reaches resolution phase (then GetToolType returns null)
        var unknownTool = new Mock<IAITool>();
        unknownTool.Setup(x => x.Name).Returns("nonexistent");
        _toolRegistry.Setup(x => x.GetAllowedTools(It.IsAny<AIQueryContext>()))
            .Returns(new List<IAITool> { unknownTool.Object });

        // GetToolType returns null => tool not found
        _toolRegistry.Setup(x => x.GetToolType("nonexistent")).Returns((Type?)null);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"action":"tool_call","tool_name":"nonexistent","parameters":{}}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.ReasoningSteps);
    }

    [Fact]
    public async Task ProcessAsync_InvalidParameters_LogsWarningAndContinues()
    {
        // Arrange
        var question = "Get tasks with bad params";
        var context = new AIQueryContext { UserId = _userId, GroupId = _groupId, Language = "vi" };

        var mockTool = new Mock<IAITool>();
        mockTool.Setup(x => x.Name).Returns("get_tasks");
        mockTool.Setup(x => x.Description).Returns("Get tasks");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(false); // Invalid!

        SetupToolForExecution(mockTool);

        _llmService.Setup(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
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
        Assert.Contains("You are a personal AI assistant", capturedPrompt);
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
        mockTool.Setup(x => x.Description).Returns("Get tasks");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject()));

        SetupToolForExecution(mockTool);

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
        mockTool.Setup(x => x.Description).Returns("Get studio stats");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AIQueryResult.Success(new JsonObject()));

        SetupToolForExecution(mockTool);

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
        mockTool.Setup(x => x.Description).Returns("Get tasks");
        mockTool.Setup(x => x.ParametersSchema).Returns(new JsonObject());
        mockTool.Setup(x => x.ValidateParameters(It.IsAny<JsonObject>())).Returns(true);
        mockTool.Setup(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        SetupToolForExecution(mockTool);

        // LLM: tool_call -> (exception caught by try/catch) -> answer
        _llmService.SetupSequence(x => x.GenerateAnswerAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync($@"{{""action"":""tool_call"",""tool_name"":""get_tasks"",""parameters"":{{""group_id"":""{_groupId}""}}}}")
            .ReturnsAsync("""{"action":"answer","final_answer":"Da xay ra loi khi lay cong viec."}""");

        // Act
        var result = await _sut.ProcessAsync(question, context);

        // Assert
        Assert.True(result.Success);
        mockTool.Verify(x => x.ExecuteAsync(
            It.IsAny<AIQueryContext>(), It.IsAny<JsonObject>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
