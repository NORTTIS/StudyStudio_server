using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for AI Question & Answer
    /// Hybrid RAG: Document context + Task statistics
    /// Route: /api/ai
    /// </summary>
    [Route("api/ai")]
    [ApiController]
    [Authorize]
    public class AIController : ControllerBase
    {
        private readonly IAIService _aiService;
        private readonly IMessageService _messageService;

        public AIController(
            IAIService aiService,
            IMessageService messageService)
        {
            _aiService = aiService;
            _messageService = messageService;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin (admin cannot use user APIs)
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// Get language from Accept-Language header
        /// Default: "vi" (Vietnamese)
        /// Supported: "vi", "en"
        /// </summary>
        private string GetLanguageFromHeader()
        {
            string? acceptLanguage = Request.Headers["Accept-Language"].FirstOrDefault();

            if (string.IsNullOrEmpty(acceptLanguage))
            {
                return "vi";
            }

            string language = acceptLanguage.Split(',')[0].Trim().ToLower();

            if (language.StartsWith("en"))
            {
                return "en";
            }

            return "vi";
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/ai/ask
        /// Ask AI about documents and tasks in group
        /// 
        /// Flow:
        /// 1. Extract language from Accept-Language header
        /// 2. Embed question (Gemini)
        /// 3. Search Qdrant (filtered by groupId)
        /// 4. Get task statistics (PostgreSQL)
        /// 5. Build context (multi-language)
        /// 6. Call Gemini LLM (2.5 Flash with 2.5 Pro fallback)
        /// 7. Return answer with remaining requests info
        /// 
        /// Headers:
        /// - Accept-Language: vi-VN,vi;q=0.9,en;q=0.8 (AI will respond in this language)
        /// 
        /// Request Body:
        /// {
        ///   "groupId": "guid",
        ///   "question": "string"
        /// }
        /// 
        /// Response:
        /// {
        ///   "answer": "string",
        ///   "sourceDocuments": [...],
        ///   "taskSummary": {...},
        ///   "processingTimeMs": number,
        ///   "generatedAt": "datetime",
        ///   "remainingRequests": number,
        ///   "dailyLimit": number
        /// }
        /// </summary>
        [HttpPost("ask")]
        public async Task<ActionResult<ApiResponse<AIAnswerResponse>>> AskQuestion(
            [FromBody] AIQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            Guid userId = ValidateAndGetUserId();
            string language = GetLanguageFromHeader();

            AIAnswerResponse result = await _aiService.AskQuestionAsync(
                userId,
                request,
                language,
                cancellationToken);

            string message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<AIAnswerResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/ai/ask/stream
        /// Ask AI about documents and tasks in group (Streaming version)
        /// 
        /// Flow:
        /// 1. Extract language from Accept-Language header
        /// 2. Embed question (Gemini)
        /// 3. Search Qdrant (filtered by groupId)
        /// 4. Get task statistics (PostgreSQL)
        /// 5. Build context (multi-language)
        /// 6. Return metadata + Stream answer from LLM
        /// 7. Log AI request after streaming completes
        /// 
        /// Headers:
        /// - Accept-Language: vi-VN,vi;q=0.9,en;q=0.8 (AI will respond in this language)
        /// 
        /// Request Body:
        /// {
        ///   "groupId": "guid",
        ///   "question": "string"
        /// }
        /// 
        /// Response: Server-Sent Events (SSE)
        /// First event (metadata):
        /// {
        ///   "sourceDocuments": [...],
        ///   "taskSummary": {...},
        ///   "generatedAt": "datetime",
        ///   "remainingRequests": number,
        ///   "dailyLimit": number
        /// }
        /// 
        /// Following events (answer chunks):
        /// data: {"chunk": "text"}
        /// </summary>
        [HttpPost("ask/stream")]
        public async Task AskQuestionStream(
            [FromBody] AIQuestionRequest request,
            CancellationToken cancellationToken = default)
        {
            Guid userId = ValidateAndGetUserId();
            string language = GetLanguageFromHeader();

            // Set headers for SSE
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");
            Response.Headers.Add("X-Accel-Buffering", "no");

            try
            {
                var (metadata, answerStream) = await _aiService.AskQuestionStreamAsync(
                    userId,
                    request,
                    language,
                    cancellationToken);

                // Send metadata first
                var metadataObj = new
                {
                    type = "metadata",
                    sourceDocuments = metadata.SourceDocuments,
                    taskSummary = metadata.TaskSummary,
                    generatedAt = metadata.GeneratedAt,
                    remainingRequests = metadata.RemainingRequests,
                    dailyLimit = metadata.DailyLimit
                };
                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(metadataObj)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);

                // Stream answer chunks
                await foreach (var chunk in answerStream.WithCancellation(cancellationToken))
                {
                    var chunkData = new { type = "chunk", content = chunk };
                    await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(chunkData)}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }

                // Send completion event
                await Response.WriteAsync("data: {\"type\": \"done\"}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (AppException ex)
            {
                var errorData = new { type = "error", code = ex.Code, message = ex.Message };
                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(errorData)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                var errorData = new { type = "error", message = ex.Message };
                await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(errorData)}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
    }
}
