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
        /// 6. Call Groq LLM with system prompt according to language
        /// 7. Return answer
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
        ///   "generatedAt": "datetime"
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
    }
}
