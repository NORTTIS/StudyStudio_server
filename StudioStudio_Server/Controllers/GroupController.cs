using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Groups
    /// Route: /api/group
    /// </summary>
    [Route("api/group")]
    [ApiController]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly IGroupService _groupService;
        private readonly IMessageService _messageService;

        public GroupController(
            IGroupService groupService,
            IMessageService messageService)
        {
            _groupService = groupService;
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
        /// [AUTHORIZED] GET /api/group
        /// Get list of groups user is member of
        /// Include: Studio info, Task count, Member count, Favourite status, Role
        /// Order by: CreatedAt DESC
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<GroupListResponse>>> GetGroups()
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.GetGroupsAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetGroup);

            return Ok(ApiResponse<GroupListResponse>.Success(
                ErrorCodes.SuccessGetGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/group/{groupId}/detail
        /// Get detailed information about group
        /// Validate: User must be member of group
        /// Include: Studio info, Members with User info, Role, Task statistics
        /// </summary>
        [HttpGet("{groupId}/detail")]
        public async Task<ActionResult<ApiResponse<GroupDetailResponse>>> GetGroupDetail(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.GetGroupDetailAsync(userId, groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupDetailResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/group/{groupId}/tasks
        /// Get paginated list of tasks in group with advanced filters
        /// Validate: User must be member of group
        /// Query Parameters:
        /// - page, pageSize: Pagination
        /// - search: Search in title and description
        /// - assigneeId: Filter by assignee
        /// - statusId: Filter by task status
        /// - priority: Filter by priority (Low=0, Medium=1, High=2)
        /// - severity: Filter by severity (Low=0, Medium=1, High=2)
        /// - startDateFrom, startDateTo: Filter by start date range
        /// - dueDateFrom, dueDateTo: Filter by due date range
        /// - sortBy: Sort field (createdAt, dueDate, startDate, priority, severity, progress)
        /// - sortAscending: Sort direction (true=ASC, false=DESC)
        /// Response includes: Task list + Group statuses for filter dropdown
        /// </summary>
        [HttpGet("{groupId}/tasks")]
        public async Task<ActionResult<ApiResponse<GroupTaskListResponse>>> GetGroupTasks(
            Guid groupId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] Guid? assigneeId = null,
            [FromQuery] Guid? statusId = null,
            [FromQuery] TaskPriority? priority = null,
            [FromQuery] TaskSeverity? severity = null,
            [FromQuery] DateTime? startDateFrom = null,
            [FromQuery] DateTime? startDateTo = null,
            [FromQuery] DateTime? dueDateFrom = null,
            [FromQuery] DateTime? dueDateTo = null,
            [FromQuery] string? sortBy = "createdAt",
            [FromQuery] bool sortAscending = true)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.GetGroupTasksAsync(
                userId,
                groupId,
                page,
                pageSize,
                search,
                assigneeId,
                statusId,
                priority,
                severity,
                startDateFrom,
                startDateTo,
                dueDateFrom,
                dueDateTo,
                sortBy,
                sortAscending);
            
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupTaskListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/group/{groupId}/members
        /// Get list of members in group
        /// Validate: User must be member of group
        /// Include: User info (FirstName, LastName, Avatar, Email), Role
        /// Order by: CreatedAt ASC (oldest member first)
        /// </summary>
        [HttpGet("{groupId}/members")]
        public async Task<ActionResult<ApiResponse<GroupMemberListResponse>>> GetGroupMembers(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.GetGroupMembersAsync(userId, groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupMemberListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/group
        /// Create new group (Studio or Independent)
        /// Validate:
        /// - Group limit according to subscription plan
        /// - Group name must not duplicate within same studio (if applicable)
        /// - User must be owner of studio (if creating within studio)
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateGroupResponse>>> CreateGroup(
            [FromBody] CreateGroupRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.CreateGroupAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateGroup);

            return Ok(ApiResponse<CreateGroupResponse>.Success(
                ErrorCodes.SuccessCreateGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/group/studio-groups
        /// Create multiple groups at once in a studio (batch create)
        /// Use case: Teacher creates multiple class groups at once
        /// Validate:
        /// - User must be owner of studio
        /// - Total groups must not exceed limit
        /// - Group names must not duplicate
        /// </summary>
        [HttpPost("studio-groups")]
        public async Task<ActionResult<ApiResponse<CreateStudioGroupsResponse>>> CreateStudioGroups(
            [FromBody] CreateStudioGroupsRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.CreateStudioGroupAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateGroup);

            return Ok(ApiResponse<CreateStudioGroupsResponse>.Success(
                ErrorCodes.SuccessCreateGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/group
        /// Update group information
        /// Validate:
        /// - User must be Owner or Moderator
        /// - Group name must not duplicate (if changing name)
        /// - Template can only be set for user-created groups
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<UpdateGroupResponse>>> UpdateGroup(
            [FromBody] UpdateGroupRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _groupService.UpdateGroupAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateGroup);

            return Ok(ApiResponse<UpdateGroupResponse>.Success(
                ErrorCodes.SuccessUpdateGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/group/{groupId}
        /// Delete (soft delete) a group
        /// Validate: User must be Owner of group
        /// Effect: Set IsActive = false
        /// </summary>
        [HttpDelete("{groupId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteGroup(Guid groupId)
        {
            var userId = ValidateAndGetUserId();
            await _groupService.DeleteGroupAsync(userId, groupId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteGroup);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteGroup,
                message,
                null));
        }
    }
}
