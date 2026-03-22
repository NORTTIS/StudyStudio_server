using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Models;
using StudioStudio_Server.Services.AI.Tools.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.AI;

/// <summary>
/// AIAgent - Bộ não AI có khả năng gọi tools để lấy data trực tiếp từ database
/// Sử dụng ReAct pattern (Reasoning + Acting)
/// </summary>
public class AIAgent
{
    private readonly IAIToolRegistry _toolRegistry;
    private readonly ILLMService _llmService;
    private readonly ILogger<AIAgent> _logger;

    // System prompt cho agent
    private readonly string _systemPromptVi;
    private readonly string _systemPromptEn;

    // Role-specific prompts
    private readonly string _personalSystemPromptVi;
    private readonly string _personalSystemPromptEn;
    private readonly string _ownerSystemPromptVi;
    private readonly string _ownerSystemPromptEn;

    // Limits
    private const int MaxToolCalls = 5; // Giới hạn số lần gọi tool để tránh infinite loop
    private const int MaxContextLength = 3000; // Giới hạn độ dài context

    public AIAgent(
        IAIToolRegistry toolRegistry,
        ILLMService llmService,
        ILogger<AIAgent> logger)
    {
        _toolRegistry = toolRegistry;
        _llmService = llmService;
        _logger = logger;

        _systemPromptVi = GetSystemPromptVi();
        _systemPromptEn = GetSystemPromptEn();
        _personalSystemPromptVi = GetPersonalSystemPromptVi();
        _personalSystemPromptEn = GetPersonalSystemPromptEn();
        _ownerSystemPromptVi = GetOwnerSystemPromptVi();
        _ownerSystemPromptEn = GetOwnerSystemPromptEn();
    }

    /// <summary>
    /// Xử lý câu hỏi với khả năng gọi tools
    /// </summary>
    public async Task<AIAgentResult> ProcessAsync(
        string userQuestion,
        AIQueryContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var history = new ToolExecutionHistory();
        var reasoningSteps = new List<string>();

        try
        {
            // Bước 1: Phân tích câu hỏi và quyết định có cần gọi tool không
            reasoningSteps.Add($"Analyzing question: {userQuestion}");

            var tools = _toolRegistry.GetAllTools();
            var toolsManifest = _toolRegistry.GetToolsManifest();

            // Bước 2: Gọi LLM với tools để quyết định action
            var systemPrompt = GetRoleSystemPrompt(context);
            
            // LLM quyết định: Trả lời trực tiếp hay gọi tool
            var decision = await DecideActionAsync(
                userQuestion,
                systemPrompt,
                toolsManifest,
                history,
                context,
                cancellationToken);

            // Bước 3: Nếu cần gọi tool, thực hiện reasoning loop
            while (decision.ShouldCallTool && decision.ToolName != null && history.Calls.Count < MaxToolCalls)
            {
                reasoningSteps.Add($"Decision: Call tool '{decision.ToolName}'");

                // Execute tool
                var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
                history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);

                if (!toolResult.IsSuccess)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");
                    break;
                }

                reasoningSteps.Add($"Tool '{decision.ToolName}' executed successfully");

                // Hỏi LLM quyết định tiếp: gọi thêm tool hay trả lời
                decision = await DecideActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true);
            }

            // Bước 4: Generate final answer
            sw.Stop();

            var result = new AIAgentResult
            {
                Answer = decision.FinalAnswer ?? "",
                ReasoningSteps = reasoningSteps,
                ToolCalls = history.Calls,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                ToolCallCount = history.Calls.Count,
                Success = true
            };

            _logger.LogInformation(
                "AIAgent completed: Question={Question}, ToolsCalled={Count}, Time={Ms}ms",
                userQuestion.Length > 50 ? userQuestion[..50] + "..." : userQuestion,
                history.Calls.Count,
                sw.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "AIAgent error");

            return new AIAgentResult
            {
                Answer = "Xin lỗi, đã xảy ra lỗi khi xử lý câu hỏi của bạn.",
                ReasoningSteps = reasoningSteps,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// LLM quyết định action tiếp theo
    /// </summary>
    private async Task<AgentDecision> DecideActionAsync(
        string userQuestion,
        string systemPrompt,
        JsonObject toolsManifest,
        ToolExecutionHistory history,
        AIQueryContext context,
        CancellationToken cancellationToken,
        bool isContinuation = false)
    {
        // Build prompt với context
        var promptBuilder = new System.Text.StringBuilder();

        if (!isContinuation)
        {
            promptBuilder.AppendLine(systemPrompt);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("=== AVAILABLE TOOLS ===");
            promptBuilder.AppendLine(toolsManifest["tools"]?.ToString() ?? "[]");
            promptBuilder.AppendLine();
        }

        promptBuilder.AppendLine("=== USER QUESTION ===");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS ===");
            foreach (var call in history.Calls)
            {
                promptBuilder.AppendLine($"Tool: {call.ToolName}");
                promptBuilder.AppendLine($"Parameters: {call.Parameters}");
                promptBuilder.AppendLine($"Result: {call.Result.ToJson()}");
                promptBuilder.AppendLine();
            }
        }

        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        if (history.Calls.Count == 0)
        {
            promptBuilder.AppendLine("- Analyze the question carefully");
            promptBuilder.AppendLine("- If you need data, call the appropriate tool(s)");
            promptBuilder.AppendLine("- If you have enough information, provide the answer directly");
            promptBuilder.AppendLine("- Format your response as JSON with 'action' (tool_call or answer) and either 'tool_name'/'parameters' or 'final_answer'");
        }
        else
        {
            promptBuilder.AppendLine("- Based on the tool results, decide:");
            promptBuilder.AppendLine("  1. Call another tool if you need more data");
            promptBuilder.AppendLine("  2. Provide the final answer if you have enough information");
            promptBuilder.AppendLine("- Format: {\"action\": \"tool_call\" or \"answer\", \"tool_name\": \"...\", \"parameters\": {...}, \"final_answer\": \"...\"}");
        }

        // Gọi LLM
        var prompt = promptBuilder.ToString();
        if (prompt.Length > MaxContextLength * 3)
        {
            prompt = prompt[..(MaxContextLength * 3)]; // Trim if too long
        }

        var response = await _llmService.GenerateAnswerAsync(
            prompt,
            userQuestion,
            "", // No extra context needed
            cancellationToken);

        // Parse response để quyết định action
        return ParseDecision(response, toolsManifest);
    }

    /// <summary>
    /// Thực thi tool
    /// </summary>
    private async Task<AIQueryResult> ExecuteToolAsync(
        string toolName,
        JsonObject parameters,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        var tool = _toolRegistry.GetTool(toolName);
        if (tool == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không tồn tại");
        }

        // Validate parameters before execution
        if (!tool.ValidateParameters(parameters))
        {
            _logger.LogWarning("Invalid parameters for tool {ToolName}: {Parameters}", toolName, parameters);
            return AIQueryResult.Error("Tham số không hợp lệ cho tool này");
        }

        try
        {
            return await tool.ExecuteAsync(context, parameters, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution error: {ToolName}", toolName);
            return AIQueryResult.Error("Đã xảy ra lỗi khi thực hiện thao tác.");
        }
    }

    /// <summary>
    /// Parse LLM response để quyết định action
    /// </summary>
    private AgentDecision ParseDecision(string response, JsonObject toolsManifest)
    {
        try
        {
            // Thử parse JSON
            var json = JsonSerializer.Deserialize<JsonElement>(response);
            
            if (json.TryGetProperty("action", out var actionElement))
            {
                var action = actionElement.GetString();
                
                if (action == "tool_call")
                {
                    var toolName = json.TryGetProperty("tool_name", out var tn) ? tn.GetString() : null;
                    var parameters = json.TryGetProperty("parameters", out var p) ? 
                        JsonSerializer.Deserialize<JsonObject>(p.GetRawText()) ?? new JsonObject() : 
                        new JsonObject();

                    return new AgentDecision
                    {
                        ShouldCallTool = !string.IsNullOrEmpty(toolName),
                        ToolName = toolName,
                        ToolParameters = parameters
                    };
                }
                else if (action == "answer")
                {
                    return new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = json.TryGetProperty("final_answer", out var fa) ? fa.GetString() : response
                    };
                }
            }

            // Fallback: nếu không parse được JSON, coi như final answer
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
        catch
        {
            // Nếu parse fail, coi response là final answer
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
    }

    private string GetSystemPromptVi() => @"Bạn là trợ lý AI của Study Studio - nền tảng học tập nhóm.

## KHẢ NĂNG ĐẶC BIỆT
Bạn có quyền truy cập vào các tools để lấy dữ liệu từ database. Khi cần thông tin cụ thể, hãy gọi tool thay vì đoán.

## CÁCH HOẠT ĐỘNG
1. Đọc câu hỏi của user
2. Xác định xem cần gọi tool nào để lấy data
3. Nếu cần data → gọi tool → nhận kết quả → quyết định tiếp
4. Nếu đủ thông tin → trả lời

## CÁC TOOLS CÓ SẴN
- get_tasks: Lấy danh sách công việc
- get_group_stats: Lấy thống kê nhóm
- get_members: Lấy danh sách thành viên
- get_deadlines: Lấy danh sách deadline

## QUY TẮC
- Chỉ gọi tool khi thực sự cần data
- Trả lời bằng tiếng Việt
- Trung thực, không bịa đặt thông tin
- Nếu data không đủ, nói rõ là không đủ thông tin

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetSystemPromptEn() => @"You are an AI assistant for Study Studio - a group learning platform.

## SPECIAL CAPABILITIES
You have access to tools to retrieve data from the database. When you need specific information, call the tool instead of guessing.

## HOW IT WORKS
1. Read user's question
2. Determine which tool(s) to call for data
3. If need data → call tool → get results → decide next step
4. If enough info → provide answer

## AVAILABLE TOOLS
- get_tasks: Get task list
- get_group_stats: Get group statistics
- get_members: Get member list
- get_deadlines: Get upcoming deadlines

## RULES
- Only call tools when you really need data
- Answer in English
- Be honest, don't fabricate information
- If data is insufficient, clearly state it

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";

    /// <summary>
    /// Returns role-specific system prompt based on AIQueryContext.
    /// - StudioId set → Master AI (Studio Owner)
    /// - GroupId set → Group AI or Personal AI (member context)
    /// - Neither set → default prompt
    /// </summary>
    private string GetRoleSystemPrompt(AIQueryContext context)
    {
        bool isEn = context.Language.ToLower() == "en";
        if (context.StudioId.HasValue)
            return isEn ? _ownerSystemPromptEn : _ownerSystemPromptVi;
        if (context.GroupId.HasValue)
            return isEn ? _personalSystemPromptEn : _personalSystemPromptVi;
        return isEn ? _systemPromptEn : _systemPromptVi;
    }

    private string GetPersonalSystemPromptVi() => @"Bạn là trợ lý AI cá nhân của Study Studio, giúp bạn quản lý công việc và tiến độ học tập.

## VAI TRÒ
Bạn là trợ lý cá nhân tập trung vào:
- Giúp bạn xem và quản lý công việc cá nhân
- Theo dõi deadline và nhắc nhở
- Tổng hợp thống kê hiệu suất cá nhân
- Gợi ý cách cải thiện năng suất

## CÁC TOOLS CÓ SẴN
- get_tasks: Lấy danh sách công việc của bạn
- get_group_stats: Lấy thống kê nhóm bạn tham gia
- get_deadlines: Lấy danh sách deadline sắp tới
- search_documents: Tìm kiếm tài liệu trong nhóm

## QUY TẮC
- Trả lời bằng tiếng Việt
- Chỉ gọi tool khi cần data cụ thể
- Trung thực, không bịa đặt
- Nếu không có quyền truy cập, nói rõ

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetPersonalSystemPromptEn() => @"You are a personal AI assistant for Study Studio, helping you manage your tasks and learning progress.

## ROLE
You are a personal assistant focused on:
- Helping you view and manage personal tasks
- Tracking deadlines and reminders
- Summarizing your personal performance statistics
- Suggesting ways to improve productivity

## AVAILABLE TOOLS
- get_tasks: Get your personal task list
- get_group_stats: Get stats of groups you belong to
- get_deadlines: Get upcoming deadlines
- search_documents: Search documents in your groups

## RULES
- Answer in English
- Only call tools when you need specific data
- Be honest, don't fabricate
- If you lack access, state it clearly

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";

    private string GetOwnerSystemPromptVi() => @"Bạn là AI Quản lý Studio (Master AI) của Study Studio - dành cho chủ sở hữu Studio.

## VAI TRÒ
Bạn có quyền truy cập toàn bộ dữ liệu Studio. Bạn tập trung vào:
- Tổng quan tất cả các nhóm trong Studio
- So sánh hiệu suất giữa các nhóm
- Phân tích rủi ro và cảnh báo sớm
- Đề xuất cải thiện cho toàn Studio
- Theo dõi dung lượng lưu trữ

## CÁC TOOLS CÓ SẴN
- get_studio_groups: Lấy danh sách tất cả nhóm trong Studio
- get_studio_analytics: Lấy thống kê tổng thể Studio
- get_group_comparison: So sánh nhiều nhóm
- get_storage_usage: Kiểm tra dung lượng lưu trữ
- get_member_permissions: Kiểm tra quyền thành viên
- get_group_stats: Thống kê chi tiết từng nhóm
- get_members: Danh sách thành viên

## QUY TẮC
- Trả lời bằng tiếng Việt
- Chỉ gọi tool khi cần data cụ thể
- Trung thực, không bịa đặt
- Khi so sánh nhóm, dùng bảng markdown để dễ đọc
- Luôn đưa ra gợi ý cải thiện cụ thể

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetOwnerSystemPromptEn() => @"You are a Studio Management AI (Master AI) for Study Studio - for Studio owners.

## ROLE
You have access to all Studio data. You focus on:
- Overview of all groups in the Studio
- Comparing performance between groups
- Risk analysis and early warnings
- Improvement recommendations for the entire Studio
- Storage usage tracking

## AVAILABLE TOOLS
- get_studio_groups: Get all groups in the Studio
- get_studio_analytics: Get overall Studio statistics
- get_group_comparison: Compare multiple groups
- get_storage_usage: Check storage quotas
- get_member_permissions: Check member permissions
- get_group_stats: Detailed group statistics
- get_members: Member list

## RULES
- Answer in English
- Only call tools when you need specific data
- Be honest, don't fabricate
- Use markdown tables for group comparisons
- Always provide specific improvement recommendations

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";
}

/// <summary>
/// Kết quả trả về từ AIAgent
/// </summary>
public class AIAgentResult
{
    public string Answer { get; set; } = "";
    public List<string> ReasoningSteps { get; set; } = new();
    public List<ToolCallEntry> ToolCalls { get; set; } = new();
    public long ProcessingTimeMs { get; set; }
    public int ToolCallCount { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Quyết định của Agent
/// </summary>
public class AgentDecision
{
    public bool ShouldCallTool { get; set; }
    public string? ToolName { get; set; }
    public JsonObject? ToolParameters { get; set; }
    public string? FinalAnswer { get; set; }
}
