using System.Text.Json.Nodes;
using StudioStudio_Server.Services.AI.Interfaces;
using StudioStudio_Server.Services.AI.Models;

namespace StudioStudio_Server.Services.AI.Pipeline;

public partial class AIAgent
{
    private bool ValidateToolResult(string toolName, AIQueryResult result, out string? validationNote)
    {
        validationNote = null;

        if (!result.IsSuccess)
        {
            validationNote = result.ErrorMessage ?? "tool failed";
            return false;
        }

        if (toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase) && result.Data != null)
        {
            if (!result.Data.TryGetPropertyValue("tasks", out var tasksNode) || tasksNode is not JsonArray tasksArr)
            {
                validationNote = "get_tasks returned no tasks array";
                return false;
            }

            validationNote = $"validated_tasks={tasksArr.Count}";
        }

        return true;
    }

    private async Task<AIQueryResult> ExecuteToolAsync(
        string toolName,
        JsonObject parameters,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("[TOOL-EXEC-START] Tool={Tool} Params={Params}", toolName, parameters.ToString());
        var toolType = _toolRegistry.GetToolType(toolName);
        if (toolType == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không tồn tại");
        }

        using var scope = _serviceProvider.CreateScope();
        var tool = scope.ServiceProvider.GetRequiredService(toolType) as IAITool;
        if (tool == null)
        {
            return AIQueryResult.Error($"Tool '{toolName}' không resolve được");
        }

        var effectiveContext = context;
        if (context.StudioId.HasValue
            && !context.GroupId.HasValue
            && IsStudioRoutedGroupTool(toolName))
        {
            var requestedGroupIdRaw = parameters.TryGetPropertyValue("group_id", out var groupIdNode)
                ? groupIdNode?.GetValue<string>()
                : null;

            if (!Guid.TryParse(requestedGroupIdRaw, out var requestedGroupId))
            {
                return AIQueryResult.Error(
                    "Master scope dang goi group tool nhung thieu/khong hop le group_id. Hay goi get_studio_groups de lay group_id GUID truoc.");
            }

            effectiveContext = new AIQueryContext
            {
                UserId = context.UserId,
                Language = context.Language,
                GroupId = requestedGroupId,
                StudioId = context.StudioId,
                SubscriptionPlan = context.SubscriptionPlan,
                StudioOwnerId = context.StudioOwnerId,
                StartTime = context.StartTime,
                SessionId = context.SessionId
            };
        }

        if (!tool.ValidateParameters(parameters))
        {
            var schema = tool.ParametersSchema;
            var neededParams = new List<string>();
            if (schema.TryGetPropertyValue("properties", out var props) && props is JsonObject)
            {
                var propsObj = (JsonObject)props;
                foreach (var prop in propsObj)
                {
                    var key = prop.Key;
                    var val = parameters.TryGetPropertyValue(key, out var v) ? v?.ToString() ?? "null" : "(missing)";
                    neededParams.Add($"{key}='{val}'");
                }
            }
            var paramsStr = string.Join(" ", neededParams);
            _logger.LogWarning(
                "[TOOL-INVALID-PARAMS] Tool={ToolName} params=[{Params}]",
                toolName, paramsStr);
            return AIQueryResult.Error($"Tham so khong hop le: {paramsStr}");
        }

        try
        {
            var result = await tool.ExecuteAsync(effectiveContext, parameters, cancellationToken);
            var resultData = result.Data?.ToString() ?? "";

            _logger.LogWarning(
                "[TOOL-RESULT] Tool={ToolName} Success={Success} TimeMs={TimeMs} Result={Result}",
                toolName, result.IsSuccess, result.ExecutionTimeMs, resultData);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool execution error: {ToolName}", toolName);
            return AIQueryResult.Error("Đã xảy ra lỗi khi thực hiện thao tác.");
        }
    }

    private static bool IsStudioRoutedGroupTool(string toolName)
    {
        return toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_stats", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_members", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_deadlines", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("search_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_performance", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_documents", StringComparison.OrdinalIgnoreCase)
            || toolName.Equals("get_group_risk", StringComparison.OrdinalIgnoreCase);
    }

    private string GetTaskPaginationSessionKey(AIQueryContext context, string toolName)
    {
        if (!context.GroupId.HasValue)
        {
            return $"ai:task_pagination:{context.UserId}:nogroup:{toolName}:{context.SessionId ?? "default"}";
        }

        var session = string.IsNullOrWhiteSpace(context.SessionId)
            ? "default"
            : context.SessionId.Trim();

        return $"ai:task_pagination:{context.UserId}:{context.GroupId.Value}:{toolName}:{session}";
    }

    private string GetTaskPaginationBaseKey(AIQueryContext context, string toolName)
    {
        if (!context.GroupId.HasValue)
        {
            return $"ai:task_pagination:{context.UserId}:nogroup:{toolName}:default";
        }

        return $"ai:task_pagination:{context.UserId}:{context.GroupId.Value}:{toolName}:default";
    }

    private static IReadOnlyList<string> GetTaskPaginationToolNames() => new[]
    {
        "get_personal_tasks",
        "get_personal_group_task"
    };

    private async Task<AITaskPaginationSessionState?> GetTaskPaginationStateAsync(AIQueryContext context)
    {
        try
        {
            var latestState = await GetTaskPaginationStateAsync(context, "get_tasks");
            foreach (var toolName in GetTaskPaginationToolNames())
            {
                var state = await GetTaskPaginationStateAsync(context, toolName);
                if (state != null && (latestState == null || state.UpdatedAt > latestState.UpdatedAt))
                {
                    latestState = state;
                }
            }

            return latestState;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FOLLOWUP] Failed to read task pagination session state");
            return null;
        }
    }

    private async Task<AITaskPaginationSessionState?> GetTaskPaginationStateAsync(AIQueryContext context, string toolName)
    {
        var key = GetTaskPaginationSessionKey(context, toolName);
        var state = await _cacheService.GetAsync<AITaskPaginationSessionState>(key);
        if (state != null)
        {
            return state;
        }

        var baseKey = GetTaskPaginationBaseKey(context, toolName);
        if (!string.Equals(baseKey, key, StringComparison.Ordinal))
        {
            state = await _cacheService.GetAsync<AITaskPaginationSessionState>(baseKey);
            if (state != null)
            {
                _logger.LogInformation(
                    "[FOLLOWUP] Session key miss, fallback to base pagination state: baseKey={BaseKey}",
                    baseKey);
                return state;
            }
        }

        return null;
    }

    private async Task SaveTaskPaginationStateIfNeededAsync(
        AIQueryContext context,
        string toolName,
        JsonObject parameters,
        AIQueryResult result)
    {
        if (!toolName.Equals("get_tasks", StringComparison.OrdinalIgnoreCase)
            && !toolName.Equals("get_personal_tasks", StringComparison.OrdinalIgnoreCase)
            && !toolName.Equals("get_personal_group_task", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!result.IsSuccess || result.Data == null)
        {
            return;
        }

        int page = 0;
        int pageSize = 20;
        int totalPages = 1;
        var normalizedToolName = toolName.Trim();

        if (result.Data.TryGetPropertyValue("current_page", out var pNode) && pNode != null)
        {
            page = pNode.GetValue<int>();
        }
        if (result.Data.TryGetPropertyValue("page_size", out var psNode) && psNode != null)
        {
            pageSize = psNode.GetValue<int>();
        }
        if (result.Data.TryGetPropertyValue("total_pages", out var tpNode) && tpNode != null)
        {
            totalPages = tpNode.GetValue<int>();
        }

        if (page <= 0)
        {
            if (parameters.TryGetPropertyValue("page", out var reqPageNode) && reqPageNode != null)
            {
                page = reqPageNode.GetValue<int>();
            }
            if (page <= 0) page = 1;
        }

        if (pageSize <= 0)
        {
            if (parameters.TryGetPropertyValue("page_size", out var reqPageSizeNode) && reqPageSizeNode != null)
            {
                pageSize = reqPageSizeNode.GetValue<int>();
            }
            if (pageSize <= 0) pageSize = 20;
        }

        if (totalPages <= 0) totalPages = 1;

        var state = new AITaskPaginationSessionState
        {
            LastPage = page,
            LastPageSize = pageSize,
            LastTotalPages = totalPages,
            LastToolName = normalizedToolName,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            var key = GetTaskPaginationSessionKey(context, normalizedToolName);
            await _cacheService.SetAsync(key, state, TimeSpan.FromMinutes(30));
            var baseKey = GetTaskPaginationBaseKey(context, normalizedToolName);
            if (!string.Equals(baseKey, key, StringComparison.Ordinal))
            {
                await _cacheService.SetAsync(baseKey, state, TimeSpan.FromMinutes(30));
            }
            _logger.LogInformation(
                "[FOLLOWUP] Saved task pagination state: key={Key} baseKey={BaseKey} page={Page}/{TotalPages} pageSize={PageSize}",
                key, baseKey, state.LastPage, state.LastTotalPages, state.LastPageSize);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[FOLLOWUP] Failed to save task pagination session state");
        }
    }

    private async Task TryExecuteTaskFollowupAsync(
        string userQuestion,
        AIQueryContext context,
        ToolExecutionHistory history,
        List<string> reasoningSteps,
        AITaskPaginationSessionState? state,
        CancellationToken cancellationToken)
    {
        if (state == null)
        {
            reasoningSteps.Add("[FOLLOWUP] No previous pagination state. Let LLM decide normally.");
            return;
        }

        var nextPage = state.LastPage + 1;
        if (nextPage > state.LastTotalPages)
        {
            nextPage = state.LastTotalPages;
        }

        var followupParams = new JsonObject
        {
            ["page"] = nextPage,
            ["page_size"] = state.LastPageSize > 0 ? state.LastPageSize : 20
        };

        var followupToolName = string.IsNullOrWhiteSpace(state.LastToolName) ? "get_tasks" : state.LastToolName;

        _logger.LogInformation(
            "[FOLLOWUP] Auto-call {Tool} for follow-up question={Question} with page={Page} pageSize={PageSize}",
            followupToolName, userQuestion, nextPage, state.LastPageSize);

        var result = await ExecuteToolAsync(followupToolName, followupParams, context, cancellationToken);
        history.AddCall(followupToolName, followupParams, result);
        await SaveTaskPaginationStateIfNeededAsync(context, followupToolName, followupParams, result);

        reasoningSteps.Add(result.IsSuccess
            ? $"[FOLLOWUP] Auto loaded {followupToolName} page={nextPage}, page_size={state.LastPageSize}"
            : $"[FOLLOWUP] Auto {followupToolName} failed: {result.ErrorMessage}");
    }

    private async Task AutoFetchDocumentContextAsync(
        string userQuestion,
        ToolExecutionHistory history,
        List<string> reasoningSteps,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        reasoningSteps.Add("[METHOD-A] Auto-fetching document context...");

        var docListParams = new JsonObject
        {
            ["limit"] = JsonValue.Create(100)
        };

        var docListResult = await ExecuteToolAsync(
            "get_group_documents",
            docListParams,
            context,
            cancellationToken);
        history.AddCall("get_group_documents", docListParams, docListResult);
        reasoningSteps.Add($"[METHOD-A] get_group_documents: {(docListResult.IsSuccess ? "OK" : $"FAIL ({docListResult.ErrorMessage})")}");
        if (docListResult.IsSuccess)
        {
            _logger.LogInformation("[AUTO-DOC] get_group_documents: OK data={Summary}", docListResult.GetDataSummary());
        }
        else
        {
            _logger.LogWarning("[AUTO-DOC] get_group_documents: FAIL error={Error}", docListResult.ErrorMessage ?? "unknown");
        }

        if (!string.IsNullOrWhiteSpace(userQuestion))
        {
            var mentionedDocNames = ExtractDocumentNamesFromQuestion(userQuestion);
            var matchedDocIds = new List<string>();

            if (mentionedDocNames.Count > 0)
            {
                matchedDocIds = MatchDocumentNamesAndExtractIds(mentionedDocNames, docListResult);
                reasoningSteps.Add($"[METHOD-A] Extracted {mentionedDocNames.Count} document name(s): {string.Join(", ", mentionedDocNames)}");

                if (matchedDocIds.Count > 0)
                {
                    reasoningSteps.Add($"[METHOD-A] Matched {matchedDocIds.Count} document ID(s). Filtering Qdrant search...");
                    _logger.LogInformation("[AUTO-DOC] Matched documents: {Ids}", string.Join(",", matchedDocIds));
                }
                else
                {
                    reasoningSteps.Add($"[METHOD-A] No matching document found for '{mentionedDocNames[0]}'. Skip broad Qdrant search.");
                    _logger.LogInformation("[AUTO-DOC] Named document not found in group docs. Skip search_documents. names={Names}", string.Join(",", mentionedDocNames));
                    return;
                }
            }

            var searchParams = new JsonObject
            {
                ["query"] = JsonValue.Create(userQuestion),
                ["top_k"] = JsonValue.Create(5)
            };

            if (matchedDocIds.Count > 0)
            {
                searchParams["document_id"] = JsonValue.Create(matchedDocIds[0]);
                reasoningSteps.Add($"[METHOD-A] search_documents with document_id={matchedDocIds[0]}, top_k=5");
            }

            var searchResult = await ExecuteToolAsync(
                "search_documents",
                searchParams,
                context,
                cancellationToken);
            history.AddCall("search_documents", searchParams, searchResult);
            reasoningSteps.Add($"[METHOD-A] search_documents: {(searchResult.IsSuccess ? "OK" : $"FAIL ({searchResult.ErrorMessage})")}");
            if (searchResult.IsSuccess)
            {
                _logger.LogInformation("[AUTO-DOC] search_documents: OK data={Summary}", searchResult.GetDataSummary());
            }
            else
            {
                _logger.LogWarning("[AUTO-DOC] search_documents: FAIL error={Error}", searchResult.ErrorMessage ?? "unknown");
            }
        }
        else
        {
            reasoningSteps.Add("[METHOD-A] search_documents: Skipped (empty question)");
        }
    }
}
