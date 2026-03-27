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
    private readonly IServiceProvider _serviceProvider;  // Resolve fresh tool instances per request
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

    /// <summary>
    /// Kiem tra xem parameters co query rong hoac null khong
    /// </summary>
    private bool HasEmptyQuery(JsonObject? p) =>
        p == null ||
        !p.TryGetPropertyValue("query", out var q) ||
        string.IsNullOrWhiteSpace(q?.GetValue<string>());

    public AIAgent(
        IAIToolRegistry toolRegistry,
        IServiceProvider serviceProvider,
        ILLMService llmService,
        ILogger<AIAgent> logger)
    {
        _toolRegistry = toolRegistry;
        _serviceProvider = serviceProvider;
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

            // Chỉ lấy tools phù hợp với role của user (Studio Owner / Group Member / Personal)
            var toolsManifest = _toolRegistry.GetToolsManifestForContext(context);

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

                // Kiểm tra tool có được phép sử dụng trong context này không
                var allowedTools = _toolRegistry.GetAllowedTools(context);
                var isToolAllowed = allowedTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));

                if (!isToolAllowed)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' not allowed for this context");
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true);
                    continue; // LLM được chọn tool khác
                }

                // Execute tool
                var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
                history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);

                if (!toolResult.IsSuccess)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");

                    // Khi search_documents that bai >= 2 lan voi query rong -> chuyen sang get_group_documents
                    if (decision.ToolName == "search_documents" &&
                        HasEmptyQuery(decision.ToolParameters!) &&
                        history.Calls.Count(c => c.ToolName == "search_documents" && HasEmptyQuery(c.Parameters)) >= 2)
                    {
                        _logger.LogWarning(
                            "[AI-DOCS-FIRST] search_documents failed {Count} times with empty query. "
                            + "Redirecting to get_group_documents to get document list first.",
                            history.Calls.Count(c => c.ToolName == "search_documents"));

                        reasoningSteps.Add("Redirecting to get_group_documents - LLM does not know available documents");
                        decision = new AgentDecision
                        {
                            ShouldCallTool = true,
                            ToolName = "get_group_documents",
                            ToolParameters = new JsonObject()
                        };
                        continue;
                    }

                    // Feed error back to LLM so it can retry with correct params
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true);
                    continue; // LLM được retry — KHÔNG break
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

            // Xác định FallbackReason
            string? fallbackReason = null;

            if (!string.IsNullOrWhiteSpace(decision?.FinalAnswer))
            {
                _logger.LogInformation(
                    "[AI-SUCCESS] ToolCalls={Count} AnswerLen={Len} Question={Q}",
                    history.Calls.Count,
                    decision.FinalAnswer.Length,
                    userQuestion);
            }
            else if (history.Calls.Count >= MaxToolCalls)
            {
                fallbackReason = $"MaxToolCalls reached ({MaxToolCalls}). LLM made no final_answer after {history.Calls.Count} tool calls.";
            }
            else if (history.Calls.Count > 0)
            {
                var last = history.Calls.Last();
                fallbackReason = last.Result.IsSuccess
                    ? $"All {history.Calls.Count} tool(s) succeeded but LLM returned empty final_answer."
                    : $"Tool '{last.ToolName}' failed: {last.Result.ErrorMessage ?? "unknown"}. LLM could not recover.";
            }
            else
            {
                fallbackReason = "LLM returned empty final_answer on first call (no tools attempted).";
            }

            // Log chi tiết khi fallback fire
            if (fallbackReason != null)
            {
                _logger.LogWarning(
                    "[AI-FALLBACK] Reason={Reason} ToolCalls={Count} Question={Q}",
                    fallbackReason,
                    history.Calls.Count,
                    userQuestion);

                foreach (var call in history.Calls)
                {
                    _logger.LogInformation(
                        "[AI-FALLBACK-TOOL] Tool={Tool} Success={Succ} Error={Err} TimeMs={Ms}",
                        call.ToolName,
                        call.Result.IsSuccess,
                        call.Result.ErrorMessage ?? "",
                        call.Result.ExecutionTimeMs);
                }
            }

            var result = new AIAgentResult
            {
                Answer = !string.IsNullOrWhiteSpace(decision?.FinalAnswer)
                    ? decision.FinalAnswer
                    : "Xin lỗi, AI không trả lời được câu hỏi này.",
                ReasoningSteps = reasoningSteps,
                ToolCalls = history.Calls,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                ToolCallCount = history.Calls.Count,
                Success = true,
                FallbackReason = fallbackReason
            };

            _logger.LogInformation(
                "AIAgent completed: Question={Question}, ToolsCalled={Count}, ToolNames={Tools}, Time={Ms}ms",
                userQuestion.Length > 50 ? userQuestion[..50] + "..." : userQuestion,
                history.Calls.Count,
                string.Join(",", history.Calls.Select(c => c.ToolName)),
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
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");
            promptBuilder.AppendLine("- search_documents REQUIREMENTS: {\"action\": \"tool_call\", \"tool_name\": \"search_documents\", \"parameters\": {\"query\": \"YOUR_SEARCH_KEYWORD_HERE\"}}");
        }
        else
        {
            promptBuilder.AppendLine("- Based on the tool results, decide:");
            promptBuilder.AppendLine("  1. Call another tool if you need more data");
            promptBuilder.AppendLine("  2. Provide the final answer if you have enough information");
            promptBuilder.AppendLine("- IMPORTANT - JSON FORMAT: Your response MUST be a valid single-line JSON object.");
            promptBuilder.AppendLine("  - Tool call: {\"action\": \"tool_call\", \"tool_name\": \"tool_name_here\", \"parameters\": {\"key\": \"value\"}}");
            promptBuilder.AppendLine("  - Final answer: {\"action\": \"answer\", \"final_answer\": \"your answer text here\"}");
            promptBuilder.AppendLine("  - NEVER omit the parameters field. If tool needs no params, use {\"parameters\": {}}");
            promptBuilder.AppendLine("- final_answer: chi la van ban thuan tuy. Khong dat trong ```, khong dat trong JSON object. Neu can xuong dong, dung \\n. Khong dung danh sach bullet dac biet.");

            // Hint: neu search_documents that bai vi query rong -> goi get_group_documents truoc
            if (history.Calls.Any(c => c.ToolName == "search_documents" && !c.Result.IsSuccess))
            {
                promptBuilder.AppendLine("- PREVIOUS FAILURE: search_documents that bai vi thieu query. "
                    + "Dieu nay xay ra vi ban khong biet trong nhom co nhung tai lieu gi. "
                    + "Goi get_group_documents (khong can tham so) de lay danh sach tai lieu co san. "
                    + "Sau do dua tren danh sach do, ban se biet phai tim kiem noi dung gi.");
            }
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

        _logger.LogInformation(
            "[LLM-RESPONSE] Step={Step} IsContinuation={IsCont} ResponseLength={Len} Response={Response}",
            history.Calls.Count,
            isContinuation,
            response.Length,
            response.Length > 300 ? response[..300] + "..." : response);

        // Parse response để quyết định action
        return ParseDecision(response, toolsManifest);
    }

    /// <summary>
    /// Thực thi tool - resolve fresh instance từ request scope để tránh DbContext disposed
    /// </summary>
    private async Task<AIQueryResult> ExecuteToolAsync(
        string toolName,
        JsonObject parameters,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        // Lấy TYPE từ registry (không dùng instance cũ)
        var toolType = _toolRegistry.GetToolType(toolName);
        if (toolType == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không tồn tại");
        }

        // Resolve fresh instance từ request scope - tránh disposed DbContext
        using var scope = _serviceProvider.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService(toolType) as IAITool;
        if (tool == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không resolve được");
        }

        // Auto-inject studio_id from context into parameters (matches pattern in each tool's ExecuteAsync)
        if (!parameters.ContainsKey("studio_id") && context.StudioId.HasValue)
        {
            parameters["studio_id"] = JsonValue.Create(context.StudioId.Value.ToString());
        }

        // Validate parameters before execution
        if (!tool.ValidateParameters(parameters))
        {
            var queryVal = parameters.TryGetPropertyValue("query", out var q) ? q?.GetValue<string>() : "(missing)";
            var topKVal = parameters.TryGetPropertyValue("top_k", out var tk) ? tk?.GetValue<string>() : "(missing)";
            var studioIdVal = parameters.TryGetPropertyValue("studio_id", out var s) ? s?.GetValue<string>() : "(missing)";
            _logger.LogWarning(
                "[TOOL-INVALID-PARAMS] Tool={ToolName} | query={Query} top_k={TopK} studio_id={StudioId}",
                toolName, queryVal ?? "(null)", topKVal, studioIdVal ?? "(not in context)");
            return AIQueryResult.Error($"Tham so khong hop le: query='{queryVal ?? "(null)"}' top_k='{topKVal}' studio_id='{studioIdVal ?? "(khong co trong context)"}'");
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
        // Guard: JSON bị cắt (streaming bị interrupt) — phát hiện bằng dấu hiệu:
        // - Chứa dấu `{` mở nhưng KHÔNG có `}` đóng hợp lệ
        // - Thường xảy ra ở cuối stream — response bị cắt giữa chừng
        if (response.Contains('{') && !response.TrimEnd().EndsWith('}'))
        {
            _logger.LogWarning(
                "[PARSE-TRUNCATED] LLM response truncated (JSON incomplete). Length={Len} Preview={Preview}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);

            // Thử extract action từ phần đã nhận được
            string? partialAction = null;
            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("\"action\""))
                {
                    var colonIdx = trimmed.IndexOf(':');
                    if (colonIdx >= 0)
                    {
                        var val = trimmed[(colonIdx + 1)..].Trim().Trim(',', '"', ' ', '{', '}');
                        if (val.StartsWith("tool_call")) partialAction = "tool_call";
                        else if (val.StartsWith("answer")) partialAction = "answer";
                    }
                }
            }

            if (partialAction == "tool_call")
            {
                // Truncated tool_call → ask LLM to retry with complete JSON
                return new AgentDecision
                {
                    ShouldCallTool = false,
                    FinalAnswer = "[INTERNAL_RETRY: JSON bi cat. Vui long goi lai tool voi JSON day du, dung format: {\"action\": \"tool_call\", \"tool_name\": \"...\", \"parameters\": {\"query\": \"...\"}}]"
                };
            }

            // Không extract được gì → fallback
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = "Xin loi, phan hoi bi cat giua chung. Vui long thu lai."
            };
        }

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
                    var fa = json.TryGetProperty("final_answer", out var faVal) ? faVal.GetString() : null;
                    return new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = !string.IsNullOrWhiteSpace(fa) ? fa : response
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
        catch (Exception ex)
        {
            // Nếu parse fail, coi response là final answer nhưng log để debug
            _logger.LogWarning(ex, "ParseDecision: LLM response is not valid JSON. Raw length: {Len}, Raw: {Raw}",
                response.Length, response.Length > 200 ? response[..200] + "..." : response);
            return new AgentDecision
            {
                ShouldCallTool = false,
                FinalAnswer = response
            };
        }
    }

    private string GetSystemPromptVi() => @"Bạn là trợ lý AI của Study Studio - nền tảng học tập nhóm.

## NGỮ CẢNH
- ĐÂY LÀ GROUP AI: user đang ở trong một nhóm cụ thể.
- group_id ĐÃ ĐƯỢC CUNG CẤP TỰ ĐỘNG bởi hệ thống. KHÔNG cần hỏi user về group_id.
- CÁC TOOL bên dưới sẽ tự động nhận group_id từ hệ thống. KHÔNG truyền group_id/studio_id trong parameters.

## CÁCH HOẠT ĐỘNG
1. Đọc câu hỏi của user
2. Xác định xem cần gọi tool nào để lấy data
3. Nếu cần data → gọi tool → nhận kết quả → quyết định tiếp
4. Nếu đủ thông tin → trả lời

## CÁC TOOLS CÓ SẴN (group_id đã được tự động cung cấp)
- get_tasks: Lấy danh sách công việc (không cần tham số)
- get_group_stats: Lấy thống kê nhóm (không cần tham số)
- get_members: Lấy danh sách thành viên (không cần tham số)
- get_deadlines: Lấy danh sách deadline (không cần tham số)
- search_documents: Tìm kiếm tài liệu. Parameters: query (bắt buộc, câu hỏi/từ khóa tìm kiếm). group_id được tự động cung cấp.
- get_group_documents: Lấy danh sách file đã upload. Không cần tham số.

## QUY TRÌNH TÌM TÀI LIỆU (QUAN TRỌNG):
1. Khi user hỏi về tài liệu mà bạn KHÔNG BIẾT trong nhóm có những file gì
   → GỌI get_group_documents TRƯỚC để lấy danh sách file có sẵn
2. Sau khi có danh sách → đánh giá file nào phù hợp với câu hỏi
3. Nếu chưa rõ (ví dụ: ""tài liệu report 3"" nhưng có nhiều file chứa ""report"")
   → HỎI USER làm rõ từ danh sách có sẵn, KHÔNG gọi search_documents ngay
4. Khi đã xác định được file cần tìm → gọi search_documents với query phù hợp

## LỖI THƯỜNG GẶP - TRÁNH XA:
- ""Tham so khong hop le"" = LLM gọi tool nhưng THIẾU hoặc SAI tham số bắt buộc (query)
- ""Khong co quyen"" = User không phải thành viên nhóm
- KHÔNG BAO GIỜ hỏi user về group_id, studio_id, hay yêu cầu cung cấp thông tin đã có sẵn

## QUY TẮC
- Chỉ gọi tool khi thực sự cần data
- Trả lời bằng tiếng Việt
- Trung thực, không bịa đặt thông tin
- Nếu không tìm thấy tài liệu, nói rõ và gợi ý từ khóa khác
- Nếu data không đủ, nói rõ là không đủ thông tin

## FORMAT TRẢ LỜI
Luôn trả lời dưới dạng JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tên_tool"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""nội dung câu trả lời""}";

    private string GetSystemPromptEn() => @"You are an AI assistant for Study Studio - a group learning platform.

## CONTEXT
- THIS IS GROUP AI: user is inside a specific group.
- group_id IS AUTOMATICALLY PROVIDED by the system. DO NOT ask user for group_id.
- Tools below will automatically receive group_id from the system. DO NOT pass group_id/studio_id in parameters.

## HOW IT WORKS
1. Read user's question
2. Determine which tool(s) to call for data
3. If need data → call tool → get results → decide next step
4. If enough info → provide answer

## AVAILABLE TOOLS (group_id auto-provided)
- get_tasks: Get task list (no parameters needed)
- get_group_stats: Get group statistics (no parameters needed)
- get_members: Get member list (no parameters needed)
- get_deadlines: Get upcoming deadlines (no parameters needed)
- search_documents: Search group documents. Parameters: query (required, search keyword/question). group_id auto-provided.
- get_group_documents: List uploaded files (no parameters needed).

## DOCUMENT SEARCH WORKFLOW (IMPORTANT):
1. When user asks about documents and you DON'T KNOW what files exist in the group
   → CALL get_group_documents FIRST to get the list of available files
2. After getting the list → evaluate which file matches the question
3. If unclear (e.g., ""report 3"" but multiple files contain ""report"")
   → ASK USER to clarify from the available list, DO NOT call search_documents yet
4. When you know which file to search → call search_documents with appropriate query

## COMMON ERRORS - AVOID:
- ""Tham so khong hop le"" = LLM called tool but MISSING or WRONG required parameter (e.g., query is null)
- ""Khong co quyen"" = User is not a member of the group
- NEVER ask user for group_id, studio_id, or information already available

## RULES
- Only call tools when you really need data
- Answer in English
- Be honest, don't fabricate information
- If no documents found, say so clearly and suggest alternative search terms
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
        // Personal AI: StudioId null + GroupId null → use Personal prompt
        if (!context.StudioId.HasValue && !context.GroupId.HasValue)
            return isEn ? _personalSystemPromptEn : _personalSystemPromptVi;
        // Group AI: GroupId set → use Group prompt (get_tasks, get_deadlines, etc.)
        return isEn ? _systemPromptEn : _systemPromptVi;
    }

    private string GetPersonalSystemPromptVi() => @"Bạn là trợ lý AI cá nhân của Study Studio, giúp bạn quản lý công việc và tiến độ học tập.

## VAI TRÒ
Bạn là trợ lý cá nhân tập trung vào:
- Giúp bạn xem và quản lý công việc cá nhân
- Theo dõi deadline và nhắc nhở
- Tổng hợp thống kê hiệu suất cá nhân
- Gợi ý cách cải thiện năng suất

## CÁC TOOLS CÓ SẴN (KHÔNG CẦN group_id)
- get_personal_tasks: Lấy danh sach tat ca cong viec (ca nhan va duoc assign)
- get_personal_deadlines: Lấy deadline cong viec ca nhan
- get_personal_stats: Lấy thong ke nang suất ca nhan

## QUY TẮC
- LUÔN gọi tool để lấy dữ liệu thực trước khi trả lời
- Trả lời bằng tiếng Việt
- Trung thực, không bịa đặt
- Nếu không có dữ liệu, nói rõ và gợi ý cách cải thiện

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

## AVAILABLE TOOLS (NO group_id REQUIRED)
- get_personal_tasks: Get all tasks (personal and assigned)
- get_personal_deadlines: Get personal task deadlines
- get_personal_stats: Get personal productivity stats

## RULES
- ALWAYS call a tool to get real data before answering
- Answer in English
- Be honest, don't fabricate
- If no data available, say so clearly

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {""key"": ""value""}}
- Final answer: {""action"": ""answer"", ""final_answer"": ""your answer""}";

    private string GetOwnerSystemPromptVi() => @"Bạn là AI Quản lý Studio (Master AI) của Study Studio - dành cho chủ sở hữu Studio.

## VAI TRÒ
Bạn có quyền truy cập toàn bộ dữ liệu của Studio. studio_id ĐÃ ĐƯỢC CUNG CẤP TỰ ĐỘNG trong request context.
Bạn tập trung vào:
- Tổng hợp tình hình tất cả các nhóm trong Studio
- So sánh hiệu suất giữa các nhóm
- Phân tích rủi ro và cảnh báo sớm
- Đề xuất cải thiện cho toàn Studio

## QUAN TRỌNG: studio_id
studio_id đã được tự động cung cấp bởi hệ thống. KHI GỌI TOOL, KHÔNG CẦN truyền studio_id:
- Tool sẽ tự động nhận studio_id từ request context
- Lỗi ""không có group_id"" / ""missing group_id"" = AI đang dùng SAI tool (group-level thay vì studio-level)
- KHÔNG BAO GIỜ gọi: get_group_stats, get_tasks, get_deadlines, get_members (cần group_id)

## CÁC TOOLS CÓ SẴN (đã có studio_id)

### Tổng hợp & Phân tích:
- get_studio_analytics: Thống kê tổng thể Studio (tổng nhóm, thành viên, task, hoàn thành, quá hạn)
  → Parameters: period (optional: ""week""/""month""/""all"", mặc định ""all"")
- get_studio_groups: Danh sách tất cả nhóm kèm thống kê task
  → Parameters: include_stats (optional, mặc định true)
- get_studio_health: Điểm sức khoẻ tổng thể Studio (0-100)
  → Parameters: (không cần tham số)

### So sánh & Đánh giá:
- get_group_comparison: So sánh nhiều nhóm với nhau
  → Parameters: metrics (optional: ""completion_rate""/""overdue""/""activity"")
- get_risk_groups: Xác định các nhóm có nguy cơ (completion < threshold)
  → Parameters: threshold (optional, mặc định 60)

### Quản lý:
- get_member_permissions: Kiểm tra quyền thành viên
  → Parameters: user_id (optional - mặc định user hiện tại)
- get_storage_usage: Kiểm tra dung lượng lưu trữ
  → Parameters: (không cần tham số)

## KHI NÀO DÙNG TOOL NÀO:
- ""tóm tắt tiến độ"" / ""overview"" / ""tổng quan"" → gọi get_studio_analytics
- ""nhóm nào"" / ""so sánh"" / ""performance"" → gọi get_group_comparison
- ""cảnh báo"" / ""nguy cơ"" / ""rủi ro"" → gọi get_risk_groups
- ""sức khoẻ"" / ""đánh giá studio"" → gọi get_studio_health
- ""danh sách nhóm"" / ""xem nhóm"" → gọi get_studio_groups

## QUY TẮC
- Trả lời bằng tiếng Việt
- KHÔNG BAO GIỜ gọi group-level tools (get_group_stats, get_tasks, get_deadlines)
- Khi cần dữ liệu → gọi studio-level tool (tự động có studio_id)
- Trung thực, không bịa đặt
- Dùng bảng markdown để so sánh nhóm
- Luôn đưa ra gợi ý cải thiện cụ thể

## FORMAT TRẢ LỜI
Nếu cần dữ liệu từ database → gọi tool:
{""action"": ""tool_call"", ""tool_name"": ""get_studio_analytics"", ""parameters"": {}}

Khi đã có đủ thông tin → trả lời trực tiếp:
{""action"": ""answer"", ""final_answer"": ""Nội dung câu trả lời bằng tiếng Việt, có thể dùng bảng markdown.""}";

    private string GetOwnerSystemPromptEn() => @"You are a Studio Management AI (Master AI) for Study Studio - for Studio owners.

## ROLE
You have access to all Studio data. studio_id is AUTOMATICALLY PROVIDED in the request context.
You focus on:
- Overview of all groups in the Studio
- Comparing performance between groups
- Risk analysis and early warnings
- Improvement recommendations for the entire Studio

## IMPORTANT: studio_id
studio_id is automatically provided by the system. WHEN CALLING TOOLS, DO NOT pass studio_id:
- Tools will automatically receive studio_id from the request context
- Error ""missing group_id"" = AI is using the WRONG tool (group-level instead of studio-level)
- NEVER call: get_group_stats, get_tasks, get_deadlines, get_members (these require group_id)

## AVAILABLE TOOLS (studio_id auto-provided)

### Analysis & Overview:
- get_studio_analytics: Overall Studio statistics (groups, members, tasks, completion, overdue)
  → Parameters: period (optional: ""week""/""month""/""all"", default ""all"")
- get_studio_groups: List all groups with task statistics
  → Parameters: include_stats (optional, default true)
- get_studio_health: Overall Studio health score (0-100)
  → Parameters: (no parameters needed)

### Comparison & Assessment:
- get_group_comparison: Compare multiple groups
  → Parameters: metrics (optional: ""completion_rate""/""overdue""/""activity"")
- get_risk_groups: Identify at-risk groups (completion < threshold)
  → Parameters: threshold (optional, default 60)

### Management:
- get_member_permissions: Check member permissions
  → Parameters: user_id (optional - defaults to current user)
- get_storage_usage: Check storage usage
  → Parameters: (no parameters needed)

## WHEN TO USE WHICH TOOL:
- ""summarize progress"" / ""overview"" → call get_studio_analytics
- ""which group"" / ""compare"" / ""performance"" → call get_group_comparison
- ""warning"" / ""risk"" / ""danger"" → call get_risk_groups
- ""health"" / ""evaluate studio"" → call get_studio_health
- ""group list"" / ""view groups"" → call get_studio_groups

## RULES
- Answer in English
- NEVER call group-level tools (get_group_stats, get_tasks, get_deadlines)
- When you need data → call studio-level tool (studio_id auto-provided)
- Be honest, don't fabricate
- Use markdown tables for group comparisons
- Always provide specific improvement recommendations

## RESPONSE FORMAT
Always respond in JSON:
- Tool call: {""action"": ""tool_call"", ""tool_name"": ""tool_name"", ""parameters"": {}}
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
    /// <summary>
    /// Lý do khi fallback fire — null nếu LLM trả lời thật.
    /// </summary>
    public string? FallbackReason { get; set; }
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
