using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for AI Question & Answer with Hybrid RAG
    /// Flow: Question → Embed → Qdrant Search → Task Stats → LLM → Response
    /// Hybrid RAG = Document Context + Task Statistics
    /// LLM: Gemini 2.5 Flash (primary) with fallback to Gemini 2.5 Pro
    /// </summary>
    public class AIService : IAIService
    {
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDatabaseService _vectorDbService;
        private readonly ITaskRepository _taskRepository;
        private readonly ILLMService _llmService;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IAIRequestLogRepository _aiRequestLogRepository;
        private readonly ILogger<AIService> _logger;

        public AIService(
            IGroupParticipantRepository groupParticipantRepository,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDbService,
            ITaskRepository taskRepository,
            ILLMService llmService,
            IUserSubscriptionRepository userSubscriptionRepository,
            IAIRequestLogRepository aiRequestLogRepository,
            ILogger<AIService> logger)
        {
            _groupParticipantRepository = groupParticipantRepository;
            _embeddingService = embeddingService;
            _vectorDbService = vectorDbService;
            _taskRepository = taskRepository;
            _llmService = llmService;
            _userSubscriptionRepository = userSubscriptionRepository;
            _aiRequestLogRepository = aiRequestLogRepository;
            _logger = logger;
        }

        /// <summary>
        /// Process user question with Hybrid RAG approach
        /// Step 0: Check AI rate limiting
        /// Step 1: Validate permission
        /// Step 2: Embed question
        /// Step 3: Search Qdrant (filtered by groupId)
        /// Step 4: Get task statistics
        /// Step 5: Build context
        /// Step 6: Call LLM
        /// Step 7: Log AI request
        /// Step 8: Return response
        /// </summary>
        public async Task<AIAnswerResponse> AskQuestionAsync(
            Guid userId,
            AIQuestionRequest request,
            string language = "vi",
            CancellationToken cancellationToken = default)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                // Step 0: Check AI rate limiting
                DateTime startOfDay = DateTime.UtcNow.Date;
                int todayRequests = await _aiRequestLogRepository.CountTodayRequestsAsync(userId, startOfDay);

                // Get user's subscription plan to check rate limit
                SubscriptionPlan? subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
                int dailyLimit = subscriptionPlan?.MaxAiRequestsPerDay ?? 20; // Default: Free Plan = 20

                if (todayRequests >= dailyLimit)
                {
                    _logger.LogWarning(
                        "AI rate limit exceeded: UserId={UserId}, Today={Today}, Limit={Limit}",
                        userId, todayRequests, dailyLimit);

                    throw new AppException(
                        ErrorCodes.AIRateLimitExceeded,
                        StatusCodes.Status429TooManyRequests);
                }

                _logger.LogInformation(
                    "AI rate limit check passed: UserId={UserId}, Requests={Today}/{Limit}",
                    userId, todayRequests, dailyLimit);

                // Step 1: Validate permission - User must be a member of the group
                bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                    request.GroupId,
                    userId);

                if (!isMember)
                {
                    throw new AppException(
                        ErrorCodes.GroupPermissionDenied,
                        StatusCodes.Status403Forbidden);
                }

                _logger.LogInformation(
                    "AI Question: UserId={UserId}, GroupId={GroupId}, Question={Question}, Language={Language}",
                    userId, request.GroupId, request.Question, language);

                // Step 2: Embed question - Convert question to 768-dimensional vector
                _logger.LogInformation("Step 2: Embedding question...");
                float[] questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    request.Question,
                    cancellationToken);

                // Step 3: Search Qdrant - Find top 3 most relevant chunks in the group
                _logger.LogInformation("Step 3: Searching Qdrant for relevant documents...");
                List<VectorSearchResponse.SearchResult> searchResults =
                    await _vectorDbService.SearchVectorsAsync(
                        questionEmbedding,
                        topK: 3,
                        groupId: request.GroupId,
                        documentId: null,
                        cancellationToken);

                _logger.LogInformation("Found {Count} relevant document chunks", searchResults.Count);

                // Step 4: Task Statistics - Get task statistics for the group
                _logger.LogInformation("Step 4: Calculating task statistics...");
                TaskSummaryResponse taskSummary = await GetTaskSummaryAsync(
                    request.GroupId,
                    cancellationToken);

                // Step 5: Build Context - Combine document context + task stats
                _logger.LogInformation("Step 5: Building context...");
                string context = BuildContext(searchResults, taskSummary, language);

                // Step 6: Call LLM (Gemini) - Generate answer
                _logger.LogInformation("Step 6: Calling Gemini LLM (2.5 Flash with 2.5 Pro fallback)...");
                string systemPrompt = GetSystemPrompt(language);
                string answer = await _llmService.GenerateAnswerAsync(
                    systemPrompt,
                    request.Question,
                    context,
                    cancellationToken);

                sw.Stop();

                // Step 7: Log AI request (estimate tokens)
                int estimatedTokens = EstimateTokenUsage(request.Question, answer, context);
                await _aiRequestLogRepository.AddAsync(new AIRequestLog
                {
                    RequestId = Guid.NewGuid(),
                    UserId = userId,
                    TokenUsed = estimatedTokens,
                    CreatedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "AI request logged: UserId={UserId}, Tokens={Tokens}, Requests today={Today}/{Limit}",
                    userId, estimatedTokens, todayRequests + 1, dailyLimit);

                // Step 8: Build Response
                AIAnswerResponse response = new AIAnswerResponse
                {
                    Answer = answer,
                    SourceDocuments = searchResults.Select(r => new SourceDocument
                    {
                        DocumentId = r.Payload.GetValueOrDefault("documentId")?.ToString(),
                        ChunkIndex = int.TryParse(
                            r.Payload.GetValueOrDefault("chunkIndex")?.ToString(),
                            out int idx) ? idx : 0,
                        RelevanceScore = r.Score,
                        Preview = TruncateText(
                            r.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                            150)
                    }).ToList(),
                    TaskSummary = taskSummary,
                    ProcessingTimeMs = sw.ElapsedMilliseconds,
                    GeneratedAt = DateTime.UtcNow,
                    RemainingRequests = dailyLimit - (todayRequests + 1),
                    DailyLimit = dailyLimit
                };

                _logger.LogInformation(
                    "AI Answer generated successfully. Time: {Ms}ms, Answer length: {Length} chars",
                    sw.ElapsedMilliseconds, answer.Length);

                return response;
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI question");
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Process user question with Hybrid RAG approach (Streaming version)
        /// Returns metadata first, then streams the answer
        /// Step 0: Check AI rate limiting
        /// Step 1: Validate permission
        /// Step 2: Embed question
        /// Step 3: Search Qdrant (filtered by groupId)
        /// Step 4: Get task statistics
        /// Step 5: Build context
        /// Step 6: Return metadata + Stream answer from LLM
        /// Step 7: Log AI request (after streaming completes)
        /// </summary>
        public async Task<(AIAnswerResponse metadata, IAsyncEnumerable<string> answerStream)> AskQuestionStreamAsync(
            Guid userId,
            AIQuestionRequest request,
            string language = "vi",
            CancellationToken cancellationToken = default)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                // Step 0: Check AI rate limiting
                DateTime startOfDay = DateTime.UtcNow.Date;
                int todayRequests = await _aiRequestLogRepository.CountTodayRequestsAsync(userId, startOfDay);

                // Get user's subscription plan to check rate limit
                SubscriptionPlan? subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(userId);
                int dailyLimit = subscriptionPlan?.MaxAiRequestsPerDay ?? 20; // Default: Free Plan = 20

                if (todayRequests >= dailyLimit)
                {
                    _logger.LogWarning(
                        "AI rate limit exceeded: UserId={UserId}, Today={Today}, Limit={Limit}",
                        userId, todayRequests, dailyLimit);

                    throw new AppException(
                        ErrorCodes.AIRateLimitExceeded,
                        StatusCodes.Status429TooManyRequests);
                }

                _logger.LogInformation(
                    "AI rate limit check passed: UserId={UserId}, Requests={Today}/{Limit}",
                    userId, todayRequests, dailyLimit);

                // Step 1: Validate permission - User must be a member of the group
                bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                    request.GroupId,
                    userId);

                if (!isMember)
                {
                    throw new AppException(
                        ErrorCodes.GroupPermissionDenied,
                        StatusCodes.Status403Forbidden);
                }

                _logger.LogInformation(
                    "AI Question (Streaming): UserId={UserId}, GroupId={GroupId}, Question={Question}, Language={Language}",
                    userId, request.GroupId, request.Question, language);

                // Step 2: Embed question - Convert question to 768-dimensional vector
                _logger.LogInformation("Step 2: Embedding question...");
                float[] questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    request.Question,
                    cancellationToken);

                // Step 3: Search Qdrant - Find top 3 most relevant chunks in the group
                _logger.LogInformation("Step 3: Searching Qdrant for relevant documents...");
                List<VectorSearchResponse.SearchResult> searchResults =
                    await _vectorDbService.SearchVectorsAsync(
                        questionEmbedding,
                        topK: 3,
                        groupId: request.GroupId,
                        documentId: null,
                        cancellationToken);

                _logger.LogInformation("Found {Count} relevant document chunks", searchResults.Count);

                // Step 4: Task Statistics - Get task statistics for the group
                _logger.LogInformation("Step 4: Calculating task statistics...");
                TaskSummaryResponse taskSummary = await GetTaskSummaryAsync(
                    request.GroupId,
                    cancellationToken);

                // Step 5: Build Context - Combine document context + task stats
                _logger.LogInformation("Step 5: Building context...");
                string context = BuildContext(searchResults, taskSummary, language);

                // Step 6: Prepare metadata and start streaming
                _logger.LogInformation("Step 6: Starting Gemini LLM streaming (2.5 Flash with 2.5 Pro fallback)...");
                string systemPrompt = GetSystemPrompt(language);

                // Prepare metadata response
                AIAnswerResponse metadata = new AIAnswerResponse
                {
                    Answer = string.Empty, // Will be filled by streaming
                    SourceDocuments = searchResults.Select(r => new SourceDocument
                    {
                        DocumentId = r.Payload.GetValueOrDefault("documentId")?.ToString(),
                        ChunkIndex = int.TryParse(
                            r.Payload.GetValueOrDefault("chunkIndex")?.ToString(),
                            out int idx) ? idx : 0,
                        RelevanceScore = r.Score,
                        Preview = TruncateText(
                            r.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                            150)
                    }).ToList(),
                    TaskSummary = taskSummary,
                    ProcessingTimeMs = 0, // Will be updated after streaming
                    GeneratedAt = DateTime.UtcNow,
                    RemainingRequests = dailyLimit - (todayRequests + 1),
                    DailyLimit = dailyLimit
                };

                // Create answer stream with logging wrapper
                var answerStream = StreamAnswerWithLoggingAsync(
                    userId,
                    request.Question,
                    context,
                    systemPrompt,
                    todayRequests,
                    dailyLimit,
                    sw,
                    cancellationToken);

                return (metadata, answerStream);
            }
            catch (AppException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI question (streaming)");
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }
        }

        /// <summary>
        /// Stream answer from LLM and log after completion
        /// </summary>
        private async IAsyncEnumerable<string> StreamAnswerWithLoggingAsync(
            Guid userId,
            string question,
            string context,
            string systemPrompt,
            int todayRequests,
            int dailyLimit,
            Stopwatch sw,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            System.Text.StringBuilder fullAnswer = new System.Text.StringBuilder();

            await foreach (var chunk in _llmService.GenerateAnswerStreamAsync(
                systemPrompt,
                question,
                context,
                cancellationToken))
            {
                fullAnswer.Append(chunk);
                yield return chunk;
            }

            sw.Stop();

            // Log AI request after streaming completes
            string answer = fullAnswer.ToString();
            int estimatedTokens = EstimateTokenUsage(question, answer, context);
            
            try
            {
                await _aiRequestLogRepository.AddAsync(new AIRequestLog
                {
                    RequestId = Guid.NewGuid(),
                    UserId = userId,
                    TokenUsed = estimatedTokens,
                    CreatedAt = DateTime.UtcNow
                });

                _logger.LogInformation(
                    "AI streaming request logged: UserId={UserId}, Tokens={Tokens}, Requests today={Today}/{Limit}, Time={Ms}ms",
                    userId, estimatedTokens, todayRequests + 1, dailyLimit, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log AI streaming request for UserId={UserId}", userId);
            }
        }

        /// <summary>
        /// Get system prompt based on language
        /// </summary>
        private string GetSystemPrompt(string language)
        {
            return language.ToLower() switch
            {
                "en" => @"You are an AI assistant for Study Studio, a group learning platform.
                Your task is to answer questions based on:
                1. Documents that have been uploaded to the group
                2. Task information for the group

                Please respond:
                - Accurately based on the provided context
                - Concisely and clearly
                - In a friendly and helpful manner
                - In English

                If the information is not available in the context, clearly state that you don't have enough information to answer.",

                _ => @"Bạn là trợ lý AI của Study Studio, một nền tảng học tập nhóm.
                Nhiệm vụ của bạn là trả lời câu hỏi dựa trên:
                1. Tài liệu (documents) đã được upload vào nhóm
                2. Thông tin về tasks (công việc) của nhóm

                Hãy trả lời một cách:
                - Chính xác dựa trên context được cung cấp
                - Ngắn gọn, súc tích
                - Thân thiện và hữu ích
                - Bằng tiếng Việt

                Nếu thông tin không có trong context, hãy nói rõ là không có đủ thông tin để trả lời."
            };
        }

        /// <summary>
        /// Get task statistics for the group from PostgreSQL
        /// Uses repository method to retrieve complete information
        /// </summary>
        private async Task<TaskSummaryResponse> GetTaskSummaryAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            return await _taskRepository.GetGroupTaskStatisticsAsync(groupId);
        }

        /// <summary>
        /// Build context string to send to LLM
        /// Combines: Document chunks + Task statistics
        /// Supports multi-language
        /// </summary>
        private string BuildContext(
            List<VectorSearchResponse.SearchResult> documents,
            TaskSummaryResponse taskSummary,
            string language)
        {
            System.Text.StringBuilder context = new System.Text.StringBuilder();

            bool isEnglish = language.ToLower() == "en";

            // Section 1: Document Context
            context.AppendLine(isEnglish ? "=== RELEVANT DOCUMENTS ===" : "=== TÀI LIỆU LIÊN QUAN ===");
            if (documents.Count > 0)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    string content = documents[i].Payload.GetValueOrDefault("content")?.ToString() ?? "";
                    string fileName = documents[i].Payload.GetValueOrDefault("fileName")?.ToString() ?? "Unknown";

                    context.AppendLine(isEnglish
                        ? $"\n[Document {i + 1}] {fileName}"
                        : $"\n[Tài liệu {i + 1}] {fileName}");
                    context.AppendLine(isEnglish
                        ? $"Relevance: {documents[i].Score:F2}"
                        : $"Độ liên quan: {documents[i].Score:F2}");
                    context.AppendLine(content);
                }
            }
            else
            {
                context.AppendLine(isEnglish
                    ? "No relevant documents found."
                    : "Không tìm thấy tài liệu liên quan.");
            }

            // Section 2: Task Statistics
            context.AppendLine(isEnglish ? "\n=== TASK STATISTICS ===" : "\n=== THỐNG KÊ CÔNG VIỆC ===");
            context.AppendLine(isEnglish
                ? $"Total tasks: {taskSummary.TotalTasks}"
                : $"Tổng số tasks: {taskSummary.TotalTasks}");
            context.AppendLine(isEnglish
                ? $"Completed: {taskSummary.CompletedTasks}/{taskSummary.TotalTasks} ({taskSummary.CompletionPercentage}%)"
                : $"Đã hoàn thành: {taskSummary.CompletedTasks}/{taskSummary.TotalTasks} ({taskSummary.CompletionPercentage}%)");
            context.AppendLine(isEnglish
                ? $"Overdue tasks: {taskSummary.OverdueTasks}"
                : $"Tasks quá hạn: {taskSummary.OverdueTasks}");

            if (taskSummary.NearestDeadline.HasValue)
            {
                context.AppendLine(isEnglish
                    ? $"Nearest deadline: {taskSummary.NearestDeadline.Value:MM/dd/yyyy HH:mm}"
                    : $"Deadline gần nhất: {taskSummary.NearestDeadline.Value:dd/MM/yyyy HH:mm}");
            }

            if (taskSummary.RiskFlags.Count > 0)
            {
                context.AppendLine(isEnglish ? "\nWarnings:" : "\nCảnh báo:");
                foreach (string flag in taskSummary.RiskFlags)
                {
                    context.AppendLine($"- {flag}");
                }
            }

            return context.ToString();
        }

        /// <summary>
        /// Truncate text to create preview
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Estimate token usage for AI request
        /// Formula: Question + Answer + Context (approx 1 token per 4 chars)
        /// Use case: Logging, analytics, potential future billing
        /// </summary>
        private int EstimateTokenUsage(string question, string answer, string context)
        {
            int totalChars = question.Length + answer.Length + context.Length;
            
            // Rough estimate: 1 token ≈ 4 characters
            int estimatedTokens = totalChars / 4;
            
            // Minimum 100 tokens (for very short requests)
            return Math.Max(estimatedTokens, 100);
        }
    }
}
