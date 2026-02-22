using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers.Admin
{
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

        [HttpPost]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> CreateTemplate([FromBody] CreateTemplateRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var template = await _templateService.CreateTemplateAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTemplate);
            return Ok(ApiResponse<TemplateResponse>.Success(ErrorCodes.SuccessCreateTemplate, message, template));
        }

        [HttpPut("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> UpdateTemplate(
            Guid templateId,
            [FromBody] UpdateTemplateRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var template = await _templateService.UpdateTemplateAsync(userId, templateId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTemplate);
            return Ok(ApiResponse<TemplateResponse>.Success(ErrorCodes.SuccessUpdateTemplate, message, template));
        }

        [HttpDelete("{templateId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(Guid templateId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            await _templateService.DeleteTemplateAsync(userId, templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTemplate);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessDeleteTemplate, message));
        }

        [HttpGet("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetTemplateById(Guid templateId)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var template = await _templateService.GetTemplateByIdAsync(templateId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<TemplateResponse>.Success(ErrorCodes.SuccessGetData, message, template));
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<TemplateResponse>>>> GetAllTemplates()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (!isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            var templates = await _templateService.GetAllTemplatesAsync();
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);
            return Ok(ApiResponse<List<TemplateResponse>>.Success(ErrorCodes.SuccessGetData, message, templates));
        }
    }
}
