using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Templates (user side)
    /// Route: /api/templates
    /// Bao g?m: L?y danh sách templates (system + user-created)
    /// </summary>
    [Route("api/templates")]
    [ApiController]
    [Authorize]
    public class UserTemplateController : ControllerBase
    {
        private readonly ITemplateService _templateService;
        private readonly IMessageService _messageService;

        public UserTemplateController(
            ITemplateService templateService,
            IMessageService messageService)
        {
            _templateService = templateService;
            _messageService = messageService;
        }

        /// <summary>
        /// Xác th?c và l?y userId t? JWT token
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

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/templates
        /// L?y danh sách templates available cho user
        /// Include: System templates + User's own templates
        /// Exclude: Templates c?a users khác
        /// S?p x?p: System templates trý?c, sau ðó CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<TemplateResponse>>>> GetAvailableTemplates()
        {
            var userId = ValidateAndGetUserId();
            var templates = await _templateService.GetAvailableTemplatesForUserAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<TemplateResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                templates));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/templates/{templateId}
        /// L?y chi ti?t m?t template
        /// Validate:
        /// - Template ph?i t?n t?i
        /// - N?u là user template ? ph?i là owner
        /// - System templates ? t?t c? users ð?u xem ðý?c
        /// </summary>
        [HttpGet("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetTemplateById(Guid templateId)
        {
            var userId = ValidateAndGetUserId();
            var template = await _templateService.GetTemplateByIdAsync(templateId);

            if (template == null)
            {
                throw new AppException(
                    ErrorCodes.TemplateNotFound,
                    StatusCodes.Status404NotFound);
            }

            if (!template.IsSystemTemplate && template.UserId != userId)
            {
                throw new AppException(
                    ErrorCodes.TemplatePermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                template));
        }
    }
}
