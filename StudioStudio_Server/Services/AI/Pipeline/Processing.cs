using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using StudioStudio_Server.Services.AI.Models;


namespace StudioStudio_Server.Services.AI.Pipeline;

public partial class AIAgent
{
    private static string BuildNeedsFixFinalAnswer(string? reviewNote)
    {
        if (string.IsNullOrWhiteSpace(reviewNote))
        {
            return "Ban can bo sung thong tin de he thong xu ly dung yeu cau.";
        }

        var cleaned = reviewNote.Replace("[needs_fix]", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(cleaned)
            ? "Ban can bo sung thong tin de he thong xu ly dung yeu cau."
            : cleaned;
    }

    public async Task<AIAgentResult> ProcessAsync(
        string userQuestion,
        AIQueryContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var history = new ToolExecutionHistory();
        var reasoningSteps = new List<string>();

        _currentTokenUsage = null;

        try
        {
            var intent = AnalyzeIntent(userQuestion, context);
            reasoningSteps.Add($"[INTENT] {intent.Summary}");

            if (intent.IsUnclearIntent)
            {
                var clarification = BuildClarificationQuestion(context);
                reasoningSteps.Add("[INTENT] Unclear intent detected -> ask user to clarify");

                return new AIAgentResult
                {
                    Answer = clarification,
                    ReasoningSteps = reasoningSteps,
                    ToolCalls = history.Calls,
                    ProcessingTimeMs = sw.ElapsedMilliseconds,
                    ToolCallCount = 0,
                    Success = true,
                    TokenUsage = _currentTokenUsage
                };
            }

            var paginationState = await GetTaskPaginationStateAsync(context);
            var isTaskFollowup = IsTaskPaginationFollowup(userQuestion);

            if (isTaskFollowup)
            {
                reasoningSteps.Add("[FOLLOWUP] Detected task pagination follow-up. Use cached task state.");
            }

            if (isTaskFollowup)
            {
                await TryExecuteTaskFollowupAsync(userQuestion, context, history, reasoningSteps, paginationState, cancellationToken);
            }

            reasoningSteps.Add($"[ANALYZE] Question={userQuestion}");

            var toolsManifest = _toolRegistry.GetToolsManifestForContext(context);
            var systemPrompt = GetRoleSystemPrompt(context);

            var decision = await PlanNextActionAsync(
                userQuestion,
                systemPrompt,
                toolsManifest,
                history,
                context,
                cancellationToken);

            var maxToolCalls = GetEffectiveMaxToolCalls();
            var parameterReviewFailures = 0;
            var consecutiveDecideWithoutExec = 0;

            while (decision.ShouldCallTool && decision.ToolName != null && history.Calls.Count < maxToolCalls && consecutiveDecideWithoutExec < MaxConsecutiveDecideWithoutExecution)
            {
                reasoningSteps.Add($"[PLAN] Call tool '{decision.ToolName}'");
                _logger.LogInformation(
                    "[AI-TOOL-DECISION] tool={Tool} params={Params}",
                    decision.ToolName, decision.ToolParameters?.ToString() ?? "{}");

                var allowedTools = _toolRegistry.GetAllowedTools(context);
                var isToolAllowed = allowedTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));

                if (!isToolAllowed)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' not allowed for this context");
                    consecutiveDecideWithoutExec++;
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                var reviewed = await ReviewPlannedToolAsync(userQuestion, context, decision, history, cancellationToken);
                decision.ToolParameters = reviewed.ToolParameters;
                _logger.LogInformation(
                    "[AI-PARAM-REVIEW] state={State} tool={Tool} accepted={Accepted} note={Note} params={Params}",
                    reviewed.ReviewState,
                    decision.ToolName,
                    reviewed.IsAccepted,
                    reviewed.ReviewNote ?? "",
                    reviewed.ToolParameters?.ToString() ?? "{}");
                if (!string.IsNullOrWhiteSpace(reviewed.ReviewNote))
                {
                    reasoningSteps.Add($"[REVIEW] {reviewed.ReviewNote}");
                }

                if (!reviewed.IsAccepted)
                {
                    parameterReviewFailures++;
                    if (!string.IsNullOrWhiteSpace(reviewed.SuggestedToolName))
                    {
                        reasoningSteps.Add($"[REVIEW] Suggested tool: {reviewed.SuggestedToolName}");

                        decision = new AgentDecision
                        {
                            ShouldCallTool = true,
                            ToolName = reviewed.SuggestedToolName,
                            ToolParameters = reviewed.SuggestedParameters
                        };
                        reasoningSteps.Add($"[REVIEW] Switching to suggested tool: {reviewed.SuggestedToolName}");
                        continue;
                    }

                    if (reviewed.ReviewState.Equals("needs_fix", StringComparison.OrdinalIgnoreCase))
                    {
                        reasoningSteps.Add("[REVIEW] Needs user clarification. Finalize without further tool planning.");
                        decision = new AgentDecision
                        {
                            ShouldCallTool = false,
                            FinalAnswer = BuildNeedsFixFinalAnswer(reviewed.ReviewNote)
                        };
                        break;
                    }

                    if (reviewed.ToolParameters != null
                        && IsNamedDocumentSearchWithoutResolvedId(userQuestion, decision, reviewed.ToolParameters))
                    {
                        reasoningSteps.Add("[REVIEW] Named document query still has no resolved document_id. Re-plan instead of broad search.");
                        consecutiveDecideWithoutExec++;
                        decision = await PlanNextActionAsync(
                            userQuestion,
                            systemPrompt,
                            toolsManifest,
                            history,
                            context,
                            cancellationToken,
                            isContinuation: true,
                            consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                        continue;
                    }

                    if (parameterReviewFailures >= 2)
                    {
                        reasoningSteps.Add("[REVIEW] Parameter review failed repeatedly, proceeding with current parameters.");
                        reviewed = reviewed with { IsAccepted = true };
                    }
                    else
                    {
                        consecutiveDecideWithoutExec++;
                        decision = await PlanNextActionAsync(
                            userQuestion,
                            systemPrompt,
                            toolsManifest,
                            history,
                            context,
                            cancellationToken,
                            isContinuation: true,
                            consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                        continue;
                    }
                }
                else
                {
                    parameterReviewFailures = 0;
                }

                var fitVerdict = ReviewToolFit(intent, decision);
                if (!fitVerdict.IsAccepted)
                {
                    reasoningSteps.Add($"[REVIEW] Tool fit rejected: {fitVerdict.ReviewNote}");
                    if (!string.IsNullOrWhiteSpace(fitVerdict.SuggestedToolName))
                    {
                        reasoningSteps.Add($"[REVIEW] Suggested tool: {fitVerdict.SuggestedToolName}");

                        decision = new AgentDecision
                        {
                            ShouldCallTool = true,
                            ToolName = fitVerdict.SuggestedToolName,
                            ToolParameters = new System.Text.Json.Nodes.JsonObject()
                        };
                        reasoningSteps.Add($"[REVIEW] Switching to suggested tool: {fitVerdict.SuggestedToolName}");
                        continue;
                    }

                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                var requestedParameters = decision.ToolParameters ?? new System.Text.Json.Nodes.JsonObject();
                var requestedParamsJson = requestedParameters.ToJsonString();
                var previousSuccessfulCall = history.Calls
                    .LastOrDefault(c => c.ToolName.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase)
                        && c.Result.IsSuccess
                        && string.Equals(c.Parameters.ToJsonString(), requestedParamsJson, StringComparison.Ordinal));
                if (previousSuccessfulCall != null)
                {
                    reasoningSteps.Add($"[GUARD] Tool '{decision.ToolName}' with same parameters already succeeded. Skip duplicate call and re-plan.");
                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                _logger.LogInformation("[TOOL-CALL] Executing tool={ToolName}", decision.ToolName);
                var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
                history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);
                consecutiveDecideWithoutExec = 0;
                await SaveTaskPaginationStateIfNeededAsync(context, decision.ToolName, decision.ToolParameters!, toolResult);

                if (toolResult.IsSuccess)
                {
                    if (!ValidateToolResult(decision.ToolName, toolResult, out var validationNote))
                    {
                        reasoningSteps.Add($"[VALIDATE] {decision.ToolName}: {validationNote}");
                        decision = await PlanNextActionAsync(
                            userQuestion,
                            systemPrompt,
                            toolsManifest,
                            history,
                            context,
                            cancellationToken,
                            isContinuation: true);
                        continue;
                    }

                    _logger.LogInformation(
                        "[AI-TOOL-OK] tool={Tool} success=true elapsedMs={Ms} data={Summary}",
                        decision.ToolName, toolResult.ExecutionTimeMs, toolResult.GetDataSummary());

                    reasoningSteps.Add($"[FLOW] {decision.ToolName} succeeded -> re-plan next step with accumulated tool results.");
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }
                else
                {
                    _logger.LogWarning(
                        "[AI-TOOL-FAIL] tool={Tool} success=false error={Error}",
                        decision.ToolName, toolResult.ErrorMessage ?? "unknown");
                }

                if (!toolResult.IsSuccess)
                {
                    reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");

                    consecutiveDecideWithoutExec++;
                    decision = await DecideActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                }
            }

            sw.Stop();

            var toolSummary = string.Join(", ", history.Calls.Select(c =>
                $"{c.ToolName}:{(c.Result.IsSuccess ? "OK" : "FAIL")}"));
            _logger.LogInformation(
                "[AI-TOOL-SUMMARY] toolsCalled={Count} tools={Summary} maxReached={Max}",
                history.Calls.Count, toolSummary, history.Calls.Count >= _config.MaxToolCalls);

            string? fallbackReason = null;
            string? finalAnswer = null;

            if (!string.IsNullOrWhiteSpace(decision?.FinalAnswer))
            {
                var synthesisSystemPrompt = BuildMarkdownSynthesisSystemPrompt(context.Language);
                finalAnswer = await _llmService.GenerateTextResponseAsync(
                    synthesisSystemPrompt,
                    BuildPromptForSynthesis(decision.FinalAnswer, history),
                    string.Empty,
                    cancellationToken);

                if (string.IsNullOrWhiteSpace(finalAnswer))
                {
                    finalAnswer = decision.FinalAnswer;
                }

                if (history.Calls.Count == 0 && LooksLikePromptLeakage(finalAnswer))
                {
                    finalAnswer = BuildClarificationQuestion(context);
                    reasoningSteps.Add("[GUARD] Prompt leakage detected with no tool data -> replaced with clarification question.");
                }

                _logger.LogInformation(
                    "[AI-SUCCESS] ToolCalls={Count} AnswerLen={Len} Question={Q}",
                    history.Calls.Count,
                    finalAnswer.Length,
                    userQuestion);
            }
            else if (history.Calls.Count >= _config.MaxToolCalls)
            {
                fallbackReason = $"MaxToolCalls reached ({maxToolCalls}). LLM made no final_answer after {history.Calls.Count} tool calls.";
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
                Answer = !string.IsNullOrWhiteSpace(finalAnswer)
                    ? finalAnswer
                    : "Xin lỗi, AI không trả lời được câu hỏi này.",
                ReasoningSteps = reasoningSteps,
                ToolCalls = history.Calls,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                ToolCallCount = history.Calls.Count,
                Success = true,
                FallbackReason = fallbackReason,
                TokenUsage = _currentTokenUsage
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

    public async IAsyncEnumerable<AIStreamChunk> ProcessStreamAsync(
        string userQuestion,
        AIQueryContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        AIStreamResult? result = null;
        Exception? caughtException = null;

        try
        {
            result = await ProcessStreamInternalAsync(userQuestion, context, cancellationToken);
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        if (caughtException != null)
        {
            _logger.LogError(caughtException, "AIAgent ProcessStreamAsync error");
            yield return new AIStreamChunk
            {
                Type = "error",
                ErrorMessage = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau."
            };
            yield break;
        }

        if (result == null)
        {
            _logger.LogError("AIAgent ProcessStreamAsync returned null result without exception");
            yield return new AIStreamChunk
            {
                Type = "error",
                ErrorMessage = "Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau."
            };
            yield break;
        }

        yield return new AIStreamChunk
        {
            Type = "metadata",
            RemainingRequests = null,
            DailyLimit = null,
            ToolCount = result.ToolCount,
            ProcessingTimeMs = result.ProcessingTimeMs,
            InputTokens = result.TokenUsage?.InputTokens,
            OutputTokens = result.TokenUsage?.OutputTokens,
            CachedTokens = result.TokenUsage?.CachedTokens,
            ThinkingTokens = result.TokenUsage?.ThinkingTokens
        };

        foreach (var chunk in result.Chunks)
        {
            yield return chunk;
        }
    }

    private async Task<AIStreamResult> ProcessStreamInternalAsync(
        string userQuestion,
        AIQueryContext context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var history = new ToolExecutionHistory();
        var reasoningSteps = new List<string>();
        var chunks = new List<AIStreamChunk>();

        _currentTokenUsage = null;

        var intent = AnalyzeIntent(userQuestion, context);
        reasoningSteps.Add($"[INTENT] {intent.Summary}");

        if (intent.IsUnclearIntent)
        {
            var clarification = BuildClarificationQuestion(context);
            reasoningSteps.Add("[INTENT] Unclear intent detected -> ask user to clarify");

            chunks.Add(new AIStreamChunk
            {
                Type = "chunk",
                Content = clarification
            });
            chunks.Add(new AIStreamChunk { Type = "done" });

            return new AIStreamResult
            {
                ToolCount = 0,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Chunks = chunks,
                TokenUsage = _currentTokenUsage
            };
        }

        var paginationState = await GetTaskPaginationStateAsync(context);
        var isTaskFollowup = IsTaskPaginationFollowup(userQuestion);

        if (isTaskFollowup)
        {
            reasoningSteps.Add("[FOLLOWUP] Detected task pagination follow-up. Use cached task state.");
            await TryExecuteTaskFollowupAsync(userQuestion, context, history, reasoningSteps, paginationState, cancellationToken);
        }

        reasoningSteps.Add($"[ANALYZE] Question={userQuestion}");

        var toolsManifest = _toolRegistry.GetToolsManifestForContext(context);
        var systemPrompt = GetRoleSystemPrompt(context);

        var decision = await PlanNextActionAsync(
            userQuestion,
            systemPrompt,
            toolsManifest,
            history,
            context,
            cancellationToken);

        var maxToolCalls = GetEffectiveMaxToolCalls();
        var parameterReviewFailures = 0;
        var consecutiveDecideWithoutExec = 0;

        while (decision.ShouldCallTool
               && decision.ToolName != null
               && history.Calls.Count < maxToolCalls
               && consecutiveDecideWithoutExec < MaxConsecutiveDecideWithoutExecution)
        {
            reasoningSteps.Add($"[PLAN] Call tool '{decision.ToolName}'");

            var allowedTools = _toolRegistry.GetAllowedTools(context);
            var isToolAllowed = allowedTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));

            if (!isToolAllowed)
            {
                reasoningSteps.Add($"Tool '{decision.ToolName}' not allowed for this context");
                consecutiveDecideWithoutExec++;
                decision = await DecideActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }

            var reviewed = await ReviewPlannedToolAsync(userQuestion, context, decision, history, cancellationToken);
            decision.ToolParameters = reviewed.ToolParameters;

            if (!reviewed.IsAccepted)
            {
                parameterReviewFailures++;
                if (!string.IsNullOrWhiteSpace(reviewed.SuggestedToolName))
                {
                    decision = new AgentDecision
                    {
                        ShouldCallTool = true,
                        ToolName = reviewed.SuggestedToolName,
                        ToolParameters = reviewed.SuggestedParameters ?? reviewed.ToolParameters
                    };
                    continue;
                }

                if (reviewed.ReviewState.Equals("needs_fix", StringComparison.OrdinalIgnoreCase))
                {
                    reasoningSteps.Add("[REVIEW] Needs user clarification. Finalize without further tool planning.");
                    decision = new AgentDecision
                    {
                        ShouldCallTool = false,
                        FinalAnswer = BuildNeedsFixFinalAnswer(reviewed.ReviewNote)
                    };
                    break;
                }

                if (reviewed.ToolParameters != null
                    && IsNamedDocumentSearchWithoutResolvedId(userQuestion, decision, reviewed.ToolParameters))
                {
                    reasoningSteps.Add("[REVIEW] Named document query still has no resolved document_id. Re-plan instead of broad search.");
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: ++consecutiveDecideWithoutExec);
                    continue;
                }

                if (parameterReviewFailures >= 2)
                {
                    reviewed = reviewed with { IsAccepted = true };
                }
                else
                {
                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }
            }
            else
            {
                parameterReviewFailures = 0;
            }

            var fitVerdict = ReviewToolFit(intent, decision);
            if (!fitVerdict.IsAccepted)
            {
                reasoningSteps.Add($"[REVIEW] Tool fit rejected: {fitVerdict.ReviewNote}");

                if (!string.IsNullOrWhiteSpace(fitVerdict.SuggestedToolName))
                {
                    decision = new AgentDecision
                    {
                        ShouldCallTool = true,
                        ToolName = fitVerdict.SuggestedToolName,
                        ToolParameters = new System.Text.Json.Nodes.JsonObject()
                    };
                    reasoningSteps.Add($"[REVIEW] Switching to suggested tool: {fitVerdict.SuggestedToolName}");
                    continue;
                }

                consecutiveDecideWithoutExec++;
                decision = await PlanNextActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }

            var requestedParameters = decision.ToolParameters ?? new System.Text.Json.Nodes.JsonObject();
            var requestedParamsJson = requestedParameters.ToJsonString();
            var previousSuccessfulCall = history.Calls
                .LastOrDefault(c => c.ToolName.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase)
                    && c.Result.IsSuccess
                    && string.Equals(c.Parameters.ToJsonString(), requestedParamsJson, StringComparison.Ordinal));
            if (previousSuccessfulCall != null)
            {
                reasoningSteps.Add($"[GUARD] Tool '{decision.ToolName}' with same parameters already succeeded. Skip duplicate call and re-plan.");
                consecutiveDecideWithoutExec++;
                decision = await PlanNextActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }

            var toolResult = await ExecuteToolAsync(decision.ToolName, decision.ToolParameters!, context, cancellationToken);
            history.AddCall(decision.ToolName, decision.ToolParameters!, toolResult);
            consecutiveDecideWithoutExec = 0;
            await SaveTaskPaginationStateIfNeededAsync(context, decision.ToolName, decision.ToolParameters!, toolResult);

            if (toolResult.IsSuccess)
            {
                if (!ValidateToolResult(decision.ToolName, toolResult, out var validationNote))
                {
                    reasoningSteps.Add($"[VALIDATE] {decision.ToolName}: {validationNote}");
                    consecutiveDecideWithoutExec++;
                    decision = await PlanNextActionAsync(
                        userQuestion,
                        systemPrompt,
                        toolsManifest,
                        history,
                        context,
                        cancellationToken,
                        isContinuation: true,
                        consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                    continue;
                }

                reasoningSteps.Add($"[FLOW] {decision.ToolName} succeeded -> re-plan next step with accumulated tool results.");
                decision = await PlanNextActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: consecutiveDecideWithoutExec);
                continue;
            }
            else
            {
                reasoningSteps.Add($"Tool '{decision.ToolName}' failed: {toolResult.ErrorMessage}");

                decision = await DecideActionAsync(
                    userQuestion,
                    systemPrompt,
                    toolsManifest,
                    history,
                    context,
                    cancellationToken,
                    isContinuation: true,
                    consecutiveDecideWithoutExecution: ++consecutiveDecideWithoutExec);
            }
        }

        sw.Stop();

        if (!string.IsNullOrWhiteSpace(decision?.FinalAnswer))
        {
            _logger.LogInformation("[AI-SYNTHESIS] Calling LLM to synthesize final answer from tool results");
            reasoningSteps.Add("[SYNTHESIS] Final answer from tool results - calling LLM to format response and suggest next steps");

            var synthesisSystemPrompt = BuildMarkdownSynthesisSystemPrompt(context.Language);
            var finalAnswer = await _llmService.GenerateTextResponseAsync(
                synthesisSystemPrompt,
                BuildPromptForSynthesis(decision.FinalAnswer, history),
                string.Empty,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(finalAnswer))
            {
                finalAnswer = decision.FinalAnswer;
            }

            if (history.Calls.Count == 0 && LooksLikePromptLeakage(finalAnswer))
            {
                finalAnswer = BuildClarificationQuestion(context);
                reasoningSteps.Add("[GUARD] Prompt leakage detected with no tool data -> replaced with clarification question.");
            }

            chunks.Add(new AIStreamChunk { Type = "chunk", Content = finalAnswer });
            chunks.Add(new AIStreamChunk { Type = "done" });
            return new AIStreamResult
            {
                ToolCount = history.Calls.Count,
                ProcessingTimeMs = sw.ElapsedMilliseconds,
                Chunks = chunks,
                TokenUsage = _currentTokenUsage
            };
        }

        var prompt = BuildPromptForFinalAnswer(
            userQuestion,
            history);

        _logger.LogInformation(
            "[LLM-STREAM] Starting streaming response. Question={Question}",
            userQuestion.Length > 50 ? userQuestion[..50] + "..." : userQuestion);

        var streamedAnswerBuilder = new StringBuilder();
        await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(
            systemPrompt,
            prompt,
            "",
            cancellationToken,
            forceTextMode: true))
        {
            if (!string.IsNullOrEmpty(chunk))
            {
                chunks.Add(new AIStreamChunk { Type = "chunk", Content = chunk });
                streamedAnswerBuilder.Append(chunk);
            }
        }

        if (history.Calls.Count == 0 && LooksLikePromptLeakage(streamedAnswerBuilder.ToString()))
        {
            chunks.Clear();
            chunks.Add(new AIStreamChunk { Type = "chunk", Content = BuildClarificationQuestion(context) });
            reasoningSteps.Add("[GUARD] Prompt leakage detected with no tool data -> replaced with clarification question.");
        }

        chunks.Add(new AIStreamChunk { Type = "done" });

        return new AIStreamResult
        {
            ToolCount = history.Calls.Count,
            ProcessingTimeMs = sw.ElapsedMilliseconds,
            Chunks = chunks,
            TokenUsage = _currentTokenUsage
        };
    }
}
