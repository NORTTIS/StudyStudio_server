using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI;
using StudioStudio_Server.Services.AI.Models;

#pragma warning disable IDE0130

namespace StudioStudio_Server.Services.AI.Pipeline;

public partial class AIAgent
{
    private bool HasEmptyQuery(JsonObject? p) =>
        p == null ||
        !p.TryGetPropertyValue("query", out var q) ||
        string.IsNullOrWhiteSpace(q?.GetValue<string>());

    private static string NormalizeText(string input)
    {
        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static bool ContainsAny(string input, params string[] phrases)
    {
        foreach (var phrase in phrases)
        {
            if (input.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTaskFilterParameters(JsonObject? parameters)
    {
        if (parameters == null) return false;

        static bool HasValue(JsonNode? n) => n != null && !string.IsNullOrWhiteSpace(n.ToString());

        return HasValue(parameters["priority"])
               || HasValue(parameters["min_priority"])
               || HasValue(parameters["severity"])
               || HasValue(parameters["min_severity"])
               || HasValue(parameters["status"])
               || HasValue(parameters["status_category"]);
    }

    private static JsonObject NormalizeGetTasksParameters(string userQuestion, JsonObject? parameters)
    {
        var normalizedParameters = parameters ?? new JsonObject();
        var question = NormalizeText(userQuestion);

        if (string.IsNullOrWhiteSpace(question))
        {
            return normalizedParameters;
        }

        var mentionsPriority = question.Contains("uu tien", StringComparison.Ordinal) || question.Contains("priority", StringComparison.Ordinal);
        var mentionsSeverity = question.Contains("muc do", StringComparison.Ordinal)
            || question.Contains("severity", StringComparison.Ordinal)
            || question.Contains("khan cap", StringComparison.Ordinal)
            || question.Contains("do khan cap", StringComparison.Ordinal)
            || question.Contains("urgency", StringComparison.Ordinal);
        var mentionsStatus = question.Contains("trang thai", StringComparison.Ordinal)
            || question.Contains("status", StringComparison.Ordinal)
            || question.Contains("hoan thanh", StringComparison.Ordinal)
            || question.Contains("dang lam", StringComparison.Ordinal)
            || question.Contains("chua bat dau", StringComparison.Ordinal)
            || question.Contains("todo", StringComparison.Ordinal);

        if (!HasTaskFilterParameters(normalizedParameters) && (mentionsPriority || mentionsSeverity || mentionsStatus))
        {
            normalizedParameters["query"] = null;
            normalizedParameters["search"] = null;
        }

        if (mentionsPriority)
        {
            if (ContainsAny(question, "tu muc cao tro len", "muc cao tro len", "high and above", "high or higher", "at least high", "from high upward"))
            {
                normalizedParameters["min_priority"] = JsonValue.Create("High");
            }
            else if (ContainsAny(question, "tu muc trung binh tro len", "muc trung binh tro len", "medium and above", "medium or higher", "at least medium", "from medium upward"))
            {
                normalizedParameters["min_priority"] = JsonValue.Create("Medium");
            }
            else if (ContainsAny(question, "uu tien cao", "priority high", "priority cao"))
            {
                normalizedParameters["priority"] = JsonValue.Create("High");
            }
            else if (ContainsAny(question, "uu tien trung binh", "priority medium"))
            {
                normalizedParameters["priority"] = JsonValue.Create("Medium");
            }
            else if (ContainsAny(question, "uu tien thap", "priority low"))
            {
                normalizedParameters["priority"] = JsonValue.Create("Low");
            }
        }

        if (mentionsSeverity)
        {
            if (ContainsAny(question, "muc do cao tro len", "muc cao tro len", "tu muc cao tro len", "do khan cap cao tro len", "khan cap cao tro len", "severity high and above", "high or higher severity", "at least major", "from major upward"))
            {
                normalizedParameters["min_severity"] = JsonValue.Create("Major");
            }
            else if (ContainsAny(question, "muc do trung binh tro len", "muc trung binh tro len", "tu muc trung binh tro len", "do khan cap trung binh tro len", "khan cap trung binh tro len", "severity moderate and above", "medium or higher severity", "at least moderate", "from moderate upward"))
            {
                normalizedParameters["min_severity"] = JsonValue.Create("Moderate");
            }
            else if (ContainsAny(question, "muc do rat cao", "do khan cap rat cao", "khan cap rat cao", "severity critical", "critical severity"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Critical");
            }
            else if (ContainsAny(question, "muc do cao", "do khan cap cao", "khan cap cao", "severity major"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Major");
            }
            else if (ContainsAny(question, "muc do trung binh", "do khan cap trung binh", "khan cap trung binh", "severity moderate"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Moderate");
            }
            else if (ContainsAny(question, "muc do thap", "do khan cap thap", "khan cap thap", "severity minor"))
            {
                normalizedParameters["severity"] = JsonValue.Create("Minor");
            }
        }

        if (mentionsStatus)
        {
            if (ContainsAny(question, "hoan thanh", "completed", "done", "finished"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("Completed");
            }
            else if (ContainsAny(question, "dang lam", "in progress", "doing"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("InProgress");
            }
            else if (ContainsAny(question, "chua bat dau", "not started", "todo"))
            {
                normalizedParameters["status_category"] = JsonValue.Create("NotStarted");
            }
        }

        return normalizedParameters;
    }

    private static bool IsTaskTool(string toolName)
    {
        return toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_stats", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_members", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_performance", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_risk", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDocumentTool(string toolName)
    {
        return toolName.Equals("get_group_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPersonalTool(string toolName)
    {
        return toolName.Equals("get_personal_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_personal_stats", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject EnsureToolParameters(JsonObject? parameters) => parameters ?? new JsonObject();

    private static List<string> ExtractDocumentNamesFromQuestion(string userQuestion)
    {
        if (string.IsNullOrWhiteSpace(userQuestion))
            return new();

        var docNames = new List<string>();

        var fileMatches = System.Text.RegularExpressions.Regex.Matches(
            userQuestion,
            @"[A-Za-z0-9_\-\. ]+\.(pdf|docx|xlsx|txt|pptx|doc|xls|ppt|jpg|png|jpeg)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in fileMatches)
        {
            var value = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !docNames.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                docNames.Add(value);
            }
        }

        if (docNames.Count == 0)
        {
            var keywordPatterns = new[] { @"file\s+([^\s,\.]+)", @"tài liệu\s+([^\s,\.]+)", @"document\s+([^\s,\.]+)" };
            foreach (var pattern in keywordPatterns)
            {
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    userQuestion,
                    pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    if (m.Groups.Count > 1)
                    {
                        var name = m.Groups[1].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(name) && !docNames.Contains(name))
                        {
                            docNames.Add(name);
                        }
                    }
                }
            }
        }

        return docNames;
    }

    private static List<string> MatchDocumentNamesAndExtractIds(
        List<string> searchNames,
        AIQueryResult docListResult)
    {
        if (searchNames.Count == 0 || !docListResult.IsSuccess || docListResult.Data == null)
            return new();

        var matchedIds = new List<string>();

        if (!docListResult.Data.TryGetPropertyValue("documents", out var docsNode) || docsNode is not JsonArray docs)
        {
            return new();
        }

        foreach (var searchName in searchNames)
        {
            var searchNameLower = searchName.ToLower();

            foreach (var doc in docs)
            {
                if (doc is not JsonObject docObj || !docObj.TryGetPropertyValue("file_name", out var fileNameNode))
                    continue;

                var fileName = fileNameNode?.GetValue<string>()?.ToLower() ?? "";

                if (fileName.Equals(searchNameLower) ||
                    fileName.Contains(searchNameLower) ||
                    searchNameLower.Contains(System.IO.Path.GetFileNameWithoutExtension(fileName)))
                {
                    string? docId = null;
                    if (docObj.TryGetPropertyValue("document_id", out var idNode))
                    {
                        var raw = idNode?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out var parsed))
                        {
                            docId = parsed.ToString();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(docId) && !matchedIds.Contains(docId))
                    {
                        matchedIds.Add(docId);
                    }
                    break;
                }
            }
        }

        return matchedIds;
    }

    private static JsonObject MergeJsonObjects(JsonObject current, JsonObject updates)
    {
        var merged = current.DeepClone() as JsonObject ?? new JsonObject();

        foreach (var kv in updates)
        {
            merged[kv.Key] = kv.Value?.DeepClone();
        }

        return merged;
    }

    private static string BuildPromptForLLMSynthesis(string toolName, JsonObject data)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Tool '{toolName}' returned the following data:");
        sb.AppendLine();
        sb.AppendLine(data.ToJsonString());
        sb.AppendLine();
        sb.AppendLine("TASK: Use the data above to answer the user's request in Vietnamese.");
        sb.AppendLine();
        sb.AppendLine("RULES:");
        sb.AppendLine("- Choose the best response format yourself: paragraph, bullets, table, or a mix.");
        sb.AppendLine("- DO NOT return JSON.");
        sb.AppendLine("- Highlight important items such as overdue tasks, high priority work, missing deadlines, or risks.");
        sb.AppendLine("- If the data suggests what to do next, include a short 'Gợi ý tiếp theo' section with 1-3 concrete actions.");
        sb.AppendLine("- Keep the answer concise but useful. Prefer readable markdown.");
        return sb.ToString();
    }

    private static string? BuildForcedAnswerFromToolResult(string toolName, JsonObject? data)
    {
        if (data == null)
        {
            return null;
        }

        return BuildPromptForLLMSynthesis(toolName, data);
    }

    private static string BuildCompactToolResultForPrompt(AIQueryResult result)
    {
        var payload = new JsonObject
        {
            ["is_success"] = JsonValue.Create(result.IsSuccess),
            ["execution_time_ms"] = JsonValue.Create(result.ExecutionTimeMs)
        };

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            payload["error_message"] = JsonValue.Create(result.ErrorMessage);
        }

        if (result.Data != null)
        {
            var compactData = result.Data.DeepClone() as JsonObject ?? new JsonObject();
            CompactTasksForPrompt(compactData);
            CompactNodeForPrompt(compactData, 0);
            payload["data"] = compactData;
        }

        var compactJson = payload.ToJsonString();
        if (compactJson.Length > MaxToolResultCharsForPrompt)
        {
            compactJson = compactJson[..MaxToolResultCharsForPrompt] + "... [truncated]";
        }

        return compactJson;
    }

    private static void CompactTasksForPrompt(JsonObject data)
    {
        if (!data.TryGetPropertyValue("tasks", out var tasksNode) || tasksNode is not JsonArray tasks)
        {
            return;
        }

        const int maxTasksForPrompt = 20;
        var slimTasks = new JsonArray();
        var keepCount = Math.Min(tasks.Count, maxTasksForPrompt);

        for (var i = 0; i < keepCount; i++)
        {
            if (tasks[i] is not JsonObject taskObj)
            {
                continue;
            }

            // Keep only the fields needed for table rendering in final response.
            slimTasks.Add(new JsonObject
            {
                ["title"] = taskObj["title"]?.DeepClone(),
                ["status"] = taskObj["status"]?.DeepClone(),
                ["priority"] = taskObj["priority"]?.DeepClone(),
                ["severity"] = taskObj["severity"]?.DeepClone(),
                ["assignee_name"] = taskObj["assignee_name"]?.DeepClone(),
                ["due_date"] = taskObj["due_date"]?.DeepClone(),
                ["is_overdue"] = taskObj["is_overdue"]?.DeepClone(),
                ["is_completed"] = taskObj["is_completed"]?.DeepClone()
            });
        }

        data["tasks"] = slimTasks;
        if (tasks.Count > keepCount)
        {
            data["tasks_truncated_count"] = JsonValue.Create(tasks.Count - keepCount);
        }
    }

    private static void CompactNodeForPrompt(JsonNode? node, int depth)
    {
        if (node == null || depth > 4)
        {
            return;
        }

        if (node is JsonObject obj)
        {
            var keys = obj.Select(kv => kv.Key).ToList();
            foreach (var key in keys)
            {
                var child = obj[key];
                if (child is JsonValue value)
                {
                    if (value.TryGetValue<string>(out var str) && str.Length > MaxStringCharsForPrompt)
                    {
                        obj[key] = JsonValue.Create(str[..MaxStringCharsForPrompt] + "...");
                    }
                    continue;
                }

                if (child is JsonArray arr)
                {
                    var maxItemsForThisArray = key.Equals("tasks", StringComparison.OrdinalIgnoreCase)
                        ? 20
                        : MaxArrayItemsForPrompt;

                    var removedCount = arr.Count - maxItemsForThisArray;
                    while (arr.Count > maxItemsForThisArray)
                    {
                        arr.RemoveAt(arr.Count - 1);
                    }

                    foreach (var item in arr)
                    {
                        CompactNodeForPrompt(item, depth + 1);
                    }

                    if (removedCount > 0)
                    {
                        obj[key + "_truncated_count"] = JsonValue.Create(removedCount);
                    }
                }
                else
                {
                    CompactNodeForPrompt(child, depth + 1);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            while (array.Count > MaxArrayItemsForPrompt)
            {
                array.RemoveAt(array.Count - 1);
            }

            foreach (var item in array)
            {
                CompactNodeForPrompt(item, depth + 1);
            }
        }
    }

    private static string BuildMarkdownSynthesisSystemPrompt(string language)
    {
        var isEnglish = language.Equals("en", StringComparison.OrdinalIgnoreCase);

        return isEnglish
            ? "You are an AI assistant that turns tool data into a clean Markdown answer. Never return JSON. Prefer concise output. If the answer contains a task list, render it as a Markdown table (columns: Task, Status, Priority, Severity, Assignee, Due date). If the answer contains a group member list, render it as a Markdown table (columns: Member, Role). Keep pagination status explicit (current page/total pages, has next page). Include a short \"Next steps\" section only when the data implies concrete actions."
            : "Ban la tro ly AI bien du lieu tu tool thanh cau tra loi Markdown sach se. Tuyet doi khong tra ve JSON. Uu tien cau tra loi ngan gon. Neu co danh sach cong viec, bat buoc trinh bay bang bang Markdown (cot: Cong viec, Trang thai, Uu tien, Muc do, Nguoi phu trach, Han chot). Neu co danh sach thanh vien nhom, trinh bay bang bang Markdown (cot: Thanh vien, Vai tro). Luon neu ro thong tin phan trang (trang hien tai/tong so trang, co trang tiep theo hay khong). Chi them muc \"Goi y tiep theo\" khi du lieu thuc su goi y hanh dong cu the.";
    }

    private static bool IsTaskPaginationFollowup(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return false;

        var q = question.Trim().ToLowerInvariant();
        return q.Contains("xem tiep")
               || q.Contains("xem tiếp")
               || q.Contains("trang tiep")
               || q.Contains("trang tiếp")
               || q.Contains("next page")
               || q == "next"
               || q == "more";
    }

    private string BuildPromptForFinalAnswer(
        string userQuestion,
        ToolExecutionHistory history)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("=== USER QUESTION ===");
        promptBuilder.AppendLine(userQuestion);
        promptBuilder.AppendLine();

        if (history.Calls.Count > 0)
        {
            promptBuilder.AppendLine("=== TOOL RESULTS ===");
            var recentCalls = history.Calls.TakeLast(3).ToList();
            foreach (var call in recentCalls)
            {
                promptBuilder.AppendLine($"Tool: {call.ToolName}");
                promptBuilder.AppendLine($"Parameters: {call.Parameters}");
                promptBuilder.AppendLine($"Result: {call.Result.ToJson()}");
                promptBuilder.AppendLine();
            }
        }

        promptBuilder.AppendLine("=== INSTRUCTIONS ===");
        promptBuilder.AppendLine("- Based on the tool results above, provide a clear and helpful answer");
        promptBuilder.AppendLine("- Format your answer in Vietnamese with proper markdown if needed");
        promptBuilder.AppendLine("- If task list data exists, present tasks as a markdown table with columns: Cong viec, Trang thai, Uu tien, Muc do, Nguoi phu trach, Han chot.");
        promptBuilder.AppendLine("- If group member list data exists, present members as a markdown table with columns: Thanh vien, Vai tro.");
        promptBuilder.AppendLine("- Always show pagination state clearly when available (current_page/total_pages/has_next_page).");
        promptBuilder.AppendLine("- If tool results are empty or insufficient, say so honestly");

        return promptBuilder.ToString();
    }
}
