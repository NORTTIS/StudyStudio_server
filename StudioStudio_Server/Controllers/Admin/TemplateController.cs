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
    /// Controller qu?n l? Templates cho Admin
    /// Route: /api/admin/templates
    /// Features: CRUD system templates
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
        /// Xác th?c user là admin và l?y userId
        /// Validate: User ph?i có IsAdmin = true
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
        /// L?y t?t c? templates (system + user-created)
        /// S?p x?p: System templates trý?c, sau ðó CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<TemplateResponse>>>> GetAllTemplates()
        {
            ValidateAdminUser();

            var templates = await _templateService.GetAllTemplatesAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<TemplateResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                templates));
        }

        /// <summary>
        /// [ADMIN] GET /api/admin/templates/{templateId}
        /// L?y chi ti?t m?t template
        /// Validate: Template ph?i t?n t?i
        /// </summary>
        [HttpGet("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetTemplateById(Guid templateId)
        {
            ValidateAdminUser();

            var template = await _templateService.GetTemplateByIdAsync(templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                template));
        }

        /// <summary>
        /// [ADMIN] POST /api/admin/templates
        /// T?o m?i system template
        /// Validate: Template name chýa t?n t?i
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
        /// C?p nh?t system template
        /// Validate:
        /// - Template ph?i t?n t?i
        /// - Template name không trùng (n?u ð?i tên)
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
        /// Xóa system template
        /// Validate:
        /// - Template ph?i t?n t?i
        /// - Template không ðang ðý?c s? d?ng b?i groups
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
    }
}
