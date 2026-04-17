using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Templates (user side)
    /// Route: /api/templates
    /// Includes: Get template list (system + user-created)
    /// </summary>
    [Route("api/templates")]
    [ApiController]
    [Authorize]
    public class UserTemplateController(
        ITemplateService templateService,
        IMessageService messageService,
        IUserService userService,
        IGroupRepository groupRepository) : ControllerBase
    {
        /// <summary>
        /// Authenticate and get userId from JWT token
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
        /// Get list of templates available for user
        /// Include: System templates + User's own templates
        /// Exclude: Templates from other users
        /// Returns: Subscription quota info + template list
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<TemplateListResponse>>> GetAvailableTemplates()
        {
            var userId = ValidateAndGetUserId();

            // Get user's subscription plan
            var subscriptionPlan = await userService.GetUserSubscriptionPlan(userId);
            var groupLimit = subscriptionPlan?.MaxGroups ?? 5;
            var memberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10;

            // Get current group count
            var groupCreated = await groupRepository.CountGroupsCreatedByUserAsync(userId);

            var templates = await templateService.GetAvailableTemplatesForUserAsync(userId);

            var response = new TemplateListResponse
            {
                Subscription = new SubscriptionQuota
                {
                    GroupLimit = groupLimit,
                    GroupCreated = groupCreated,
                    MemberLimit = memberLimit
                },
                Templates = templates
            };

            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TemplateListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/templates/{templateId}
        /// Get template details
        /// Validate:
        /// - Template must exist
        /// - If user template → must be owner
        /// - System templates → all users can view
        /// </summary>
        [HttpGet("{templateId}")]
        public async Task<ActionResult<ApiResponse<TemplateResponse>>> GetTemplateById(Guid templateId)
        {
            var userId = ValidateAndGetUserId();
            var template = await templateService.GetTemplateByIdAsync(templateId);

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

            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<TemplateResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                template));
        }
    }
}
