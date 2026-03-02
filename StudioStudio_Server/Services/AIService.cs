using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? AI Question & Answer v?i Hybrid RAG
    /// Flow: Question ? Embed ? Qdrant Search ? Task Stats ? LLM ? Response
    /// Hybrid RAG = Document Context + Task Statistics
    /// </summary>
    public class AIService : IAIService
    {
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDatabaseService _vectorDbService;
        private readonly ITaskRepository _taskRepository;
        private readonly ILLMService _llmService;
        private readonly ILogger<AIService> _logger;

        // System prompt ð?nh ngh?a behavior c?a AI
        private const string SYSTEM_PROMPT = @"B?n là tr? l? AI c?a Study Studio, m?t n?n t?ng h?c t?p nhóm.
Nhi?m v? c?a b?n là tr? l?i câu h?i d?a trên:
1. Tài li?u (documents) ð? ðý?c upload vào nhóm
2. Thông tin v? tasks (công vi?c) c?a nhóm

H?y tr? l?i m?t cách:
- Chính xác d?a trên context ðý?c cung c?p
- Ng?n g?n, súc tích
- Thân thi?n và h?u ích
- B?ng ti?ng Vi?t

N?u thông tin không có trong context, h?y nói r? là không có ð? thông tin ð? tr? l?i.";

        public AIService(
            IGroupParticipantRepository groupParticipantRepository,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDbService,
            ITaskRepository taskRepository,
            ILLMService llmService,
            ILogger<AIService> logger)
        {
            _groupParticipantRepository = groupParticipantRepository;
            _embeddingService = embeddingService;
            _vectorDbService = vectorDbService;
            _taskRepository = taskRepository;
            _llmService = llmService;
            _logger = logger;
        }

        /// <summary>
        /// X? l? câu h?i t? user v?i Hybrid RAG approach
        /// Step 1: Validate permission
        /// Step 2: Embed question
        /// Step 3: Search Qdrant (filtered by groupId)
        /// Step 4: Get task statistics
        /// Step 5: Build context
        /// Step 6: Call LLM
        /// Step 7: Return response
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
                // Step 1: Validate permission - User ph?i là member c?a group
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

                // Step 2: Embed question - Chuy?n câu h?i thành vector 768 dimensions
                _logger.LogInformation("Step 2: Embedding question...");
                float[] questionEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    request.Question,
                    cancellationToken);

                // Step 3: Search Qdrant - T?m top 3 chunks liên quan nh?t trong group
                _logger.LogInformation("Step 3: Searching Qdrant for relevant documents...");
                List<VectorSearchResponse.SearchResult> searchResults =
                    await _vectorDbService.SearchVectorsAsync(
                        questionEmbedding,
                        topK: 3,
                        groupId: request.GroupId,
                        cancellationToken);

                _logger.LogInformation("Found {Count} relevant document chunks", searchResults.Count);

                // Step 4: Task Statistics - L?y th?ng kê tasks c?a group
                _logger.LogInformation("Step 4: Calculating task statistics...");
                TaskSummaryResponse taskSummary = await GetTaskSummaryAsync(
                    request.GroupId,
                    cancellationToken);

                // Step 5: Build Context - K?t h?p document context + task stats
                _logger.LogInformation("Step 5: Building context...");
                string context = BuildContext(searchResults, taskSummary, language);

                // Step 6: Call LLM (Groq) - Generate answer
                _logger.LogInformation("Step 6: Calling Groq LLM...");
                string systemPrompt = GetSystemPrompt(language);
                string answer = await _llmService.GenerateAnswerAsync(
                    systemPrompt,
                    request.Question,
                    context,
                    cancellationToken);

                sw.Stop();

                // Step 7: Build Response
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
                    GeneratedAt = DateTime.UtcNow
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
        /// L?y system prompt theo ngôn ng?
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

                _ => @"B?n là tr? l? AI c?a Study Studio, m?t n?n t?ng h?c t?p nhóm.
Nhi?m v? c?a b?n là tr? l?i câu h?i d?a trên:
1. Tài li?u (documents) ð? ðý?c upload vào nhóm
2. Thông tin v? tasks (công vi?c) c?a nhóm

H?y tr? l?i m?t cách:
- Chính xác d?a trên context ðý?c cung c?p
- Ng?n g?n, súc tích
- Thân thi?n và h?u ích
- B?ng ti?ng Vi?t

N?u thông tin không có trong context, h?y nói r? là không có ð? thông tin ð? tr? l?i."
            };
        }

        /// <summary>
        /// L?y th?ng kê tasks c?a group t? PostgreSQL
        /// S? d?ng repository method ð? l?y thông tin ð?y ð?
        /// </summary>
        private async Task<TaskSummaryResponse> GetTaskSummaryAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            return await _taskRepository.GetGroupTaskStatisticsAsync(groupId);
        }

        /// <summary>
        /// Build context string ð? g?i cho LLM
        /// K?t h?p: Document chunks + Task statistics
        /// Support multi-language
        /// </summary>
        private string BuildContext(
            List<VectorSearchResponse.SearchResult> documents,
            TaskSummaryResponse taskSummary,
            string language)
        {
            System.Text.StringBuilder context = new System.Text.StringBuilder();

            bool isEnglish = language.ToLower() == "en";

            // Section 1: Document Context
            context.AppendLine(isEnglish ? "=== RELEVANT DOCUMENTS ===" : "=== TÀI LI?U LIÊN QUAN ===");
            if (documents.Count > 0)
            {
                for (int i = 0; i < documents.Count; i++)
                {
                    string content = documents[i].Payload.GetValueOrDefault("content")?.ToString() ?? "";
                    string fileName = documents[i].Payload.GetValueOrDefault("fileName")?.ToString() ?? "Unknown";

                    context.AppendLine(isEnglish 
                        ? $"\n[Document {i + 1}] {fileName}" 
                        : $"\n[Tài li?u {i + 1}] {fileName}");
                    context.AppendLine(isEnglish 
                        ? $"Relevance: {documents[i].Score:F2}" 
                        : $"Ð? liên quan: {documents[i].Score:F2}");
                    context.AppendLine(content);
                }
            }
            else
            {
                context.AppendLine(isEnglish 
                    ? "No relevant documents found." 
                    : "Không t?m th?y tài li?u liên quan.");
            }

            // Section 2: Task Statistics
            context.AppendLine(isEnglish ? "\n=== TASK STATISTICS ===" : "\n=== TH?NG KÊ CÔNG VI?C ===");
            context.AppendLine(isEnglish 
                ? $"Total tasks: {taskSummary.TotalTasks}" 
                : $"T?ng s? tasks: {taskSummary.TotalTasks}");
            context.AppendLine(isEnglish 
                ? $"Completed: {taskSummary.CompletedTasks}/{taskSummary.TotalTasks} ({taskSummary.CompletionPercentage}%)" 
                : $"Ð? hoàn thành: {taskSummary.CompletedTasks}/{taskSummary.TotalTasks} ({taskSummary.CompletionPercentage}%)");
            context.AppendLine(isEnglish 
                ? $"Overdue tasks: {taskSummary.OverdueTasks}" 
                : $"Tasks quá h?n: {taskSummary.OverdueTasks}");

            if (taskSummary.NearestDeadline.HasValue)
            {
                context.AppendLine(isEnglish 
                    ? $"Nearest deadline: {taskSummary.NearestDeadline.Value:MM/dd/yyyy HH:mm}" 
                    : $"Deadline g?n nh?t: {taskSummary.NearestDeadline.Value:dd/MM/yyyy HH:mm}");
            }

            if (taskSummary.RiskFlags.Count > 0)
            {
                context.AppendLine(isEnglish ? "\nWarnings:" : "\nC?nh báo:");
                foreach (string flag in taskSummary.RiskFlags)
                {
                    context.AppendLine($"- {flag}");
                }
            }

            return context.ToString();
        }

        /// <summary>
        /// Truncate text ð? t?o preview
        /// </summary>
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            {
                return text;
            }

            return text.Substring(0, maxLength) + "...";
        }
    }
}
