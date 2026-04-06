using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for managing Templates
    /// Route: /api/admin/templates
    /// </summary>
    [Route("api/admin/templates")]
    [ApiController]
    [Authorize]
    public class TemplateController : ControllerBase
    {
        private readonly ITemplateService _templateService;
        private readonly IMessageService _messageService;

        public TemplateController(
            ITemplateService templateService,
            IMessageService messageService)
        {
            _templateService = templateService;
            _messageService = messageService;
        }

        /// <summary>
        /// Validate user is admin
        /// Throw 403 if not admin
        /// </summary>
        private Guid ValidateAdminUser()
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

            if (!isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/templates
        /// Get all system templates (including inactive) for admin management
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<TemplateResponse>>>> GetAllTemplates()
        {
            ValidateAdminUser();

            var templates = await _templateService.GetAllSystemTemplatesAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<TemplateResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                templates));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/templates/{templateId}
        /// Get template details (including inactive)
        /// </summary>
        [HttpGet("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetTemplateById(Guid templateId)
        {
            ValidateAdminUser();

            var template = await _templateService.GetTemplateByIdIncludingInactiveAsync(templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                template));
        }

        /// <summary>
        /// [ADMIN] POST /api/admin/templates
        /// Create new system template
        /// Validate: Template name must not exist
        /// Auto-set: IsSystemTemplate = true, CreatedBy = adminUserId
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> CreateTemplate(
            [FromBody] CreateTemplateRequest request)
        {
            var userId = ValidateAdminUser();

            var template = await _templateService.CreateTemplateAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTemplate);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessCreateTemplate,
                message,
                template));
        }

        /// <summary>
        /// [ADMIN] PUT /api/admin/templates/{templateId}
        /// Update system template
        /// Validate:
        /// - Template must exist
        /// - Template name must not duplicate (if changing name)
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        [HttpPut("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> UpdateTemplate(
            Guid templateId,
            [FromBody] UpdateTemplateRequest request)
        {
            var userId = ValidateAdminUser();

            var template = await _templateService.UpdateTemplateAsync(userId, templateId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTemplate);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessUpdateTemplate,
                message,
                template));
        }

        /// <summary>
        /// [ADMIN] DELETE /api/admin/templates/{templateId}
        /// Inactive (soft-delete) system template
        /// </summary>
        [HttpDelete("{templateId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(Guid templateId)
        {
            var userId = ValidateAdminUser();

            await _templateService.DeleteTemplateAsync(userId, templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTemplate);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTemplate,
                message,
                null));
        }

        /// <summary>
        /// [ADMIN] DELETE /api/admin/templates/{templateId}/hard
        /// Hard-delete template permanently (xóa cứng: template + group + groupTaskStatuses)
        /// </summary>
        [HttpDelete("{templateId}/hard")]
        public async Task<ActionResult<ApiResponse<object>>> HardDeleteTemplate(Guid templateId)
        {
            ValidateAdminUser();

            await _templateService.HardDeleteTemplateAsync(templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTemplate);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTemplate,
                message,
                null));
        }
    }
}
