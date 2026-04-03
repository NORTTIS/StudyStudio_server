using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Studios
    /// Route: /api/studio
    /// </summary>
    [Route("api/studio")]
    [ApiController]
    [Authorize]
    public class StudioController : ControllerBase
    {
        private readonly IStudioService _studioService;
        private readonly IGroupService _groupService;
        private readonly IMessageService _messageService;
        private readonly IBatchAssignService _batchAssignService;

        public StudioController(
            IStudioService studioService,
            IGroupService groupService,
            IMessageService messageService,
            IBatchAssignService batchAssignService)
        {
            _studioService = studioService;
            _groupService = groupService;
            _messageService = messageService;
            _batchAssignService = batchAssignService;
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio
        /// Get list of studios owned by user
        /// Condition: OwnerId = userId
        /// Order by: CreatedAt DESC
        /// Include: GroupCount for each studio + Subscription info
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<StudioListResponse>>> GetUserStudios()
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.GetUserStudiosAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}
        /// Get studio details by ID
        /// Validate: User must be owner of studio
        /// Include: Studio info + GroupCount
        /// </summary>
        [HttpGet("{studioId}")]
        public async Task<ActionResult<ApiResponse<StudioResponse>>> GetStudioDetail(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.GetStudioDetailAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}/groups
        /// Get list of groups in studio
        /// Validate: User must be owner of studio
        /// Order by: Groups by UpdatedAt DESC
        /// Include: Studio info + list of groups
        /// </summary>
        [HttpGet("{studioId}/groups")]
        public async Task<ActionResult<ApiResponse<StudioGroupListResponse>>> ViewStudioGroupList(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _groupService.GetStudioGroupsAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetGroup);

            return Ok(ApiResponse<StudioGroupListResponse>.Success(
                ErrorCodes.SuccessGetGroup,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio
        /// Create new studio
        /// Validate: Studio limit according to subscription plan
        /// Auto-set: CreatedAt, UpdatedAt = UtcNow
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<StudioResponse>>> CreateNewStudio(
            [FromBody] CreateStudioRequest request)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.CreateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateStudio);

            return Ok(ApiResponse<StudioResponse>.Success(
                ErrorCodes.SuccessCreateStudio,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] PUT /api/studio
        /// Update studio information
        /// Validate:
        /// - Studio must exist
        /// - User must be owner of studio
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<UpdateStudioResponse>>> UpdateStudio(
            [FromBody] UpdateStudioRequest request)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.UpdateStudioAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateStudio);

            return Ok(ApiResponse<UpdateStudioResponse>.Success(
                ErrorCodes.SuccessUpdateStudio,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/studio/{studioId}
        /// Delete (soft delete) a studio
        /// Validate:
        /// - Studio must exist
        /// - User must be owner of studio
        /// Effect: Set IsActive = false (or DeletedFlag = true)
        /// </summary>
        [HttpDelete("{studioId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteStudio(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            await _studioService.DeleteStudioAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteStudio);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteStudio,
                message,
                null));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}/members
        /// Get list of members in studio
        /// Validate: User must be member or owner of studio
        /// Include: User info, studio role, and group info within this studio
        /// </summary>
        [HttpGet("{studioId}/members")]
        public async Task<ActionResult<ApiResponse<List<StudioMemberResponse>>>> GetStudioMembers(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.GetStudioMembersAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<List<StudioMemberResponse>>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/studio/{studioId}/leave
        /// Leave a studio (self-remove)
        /// Validate:
        /// - Studio must exist
        /// - User must be a member of the studio
        /// - Owner cannot leave
        /// </summary>
        [HttpDelete("{studioId}/leave")]
        public async Task<ActionResult<ApiResponse<LeaveStudioResponse>>> LeaveStudio(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.LeaveStudioAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessLeaveStudio);

            return Ok(ApiResponse<LeaveStudioResponse>.Success(
                ErrorCodes.SuccessLeaveStudio,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio/{studioId}/members/batch-assign
        /// Upload CSV/Excel file to batch assign members to groups
        /// File format: Email, GroupName, Role (columns)
        /// Valid roles: Member, Moderator, Commenter, Viewer
        /// Owner role is not allowed
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        /// <param name="file">CSV or Excel file (.csv, .xlsx)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [HttpPost("{studioId}/members/batch-assign")]
        [RequestSizeLimit(5 * 1024 * 1024)] // 5MB limit
        public async Task<ActionResult<ApiResponse<BatchAssignResponse>>> BatchAssign(
            Guid studioId,
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);

            if (file == null || file.Length == 0)
            {
                throw new AppException(ErrorCodes.ValidationRequiredField, StatusCodes.Status400BadRequest);
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new AppException(ErrorCodes.ValidationFileTooLarge, StatusCodes.Status400BadRequest);
            }

            using var stream = file.OpenReadStream();
            var result = await _batchAssignService.BatchAssignAsync(
                studioId,
                userId,
                stream,
                file.FileName,
                cancellationToken);

            var message = _messageService.GetMessage(ErrorCodes.SuccessBatchAssign);
            return Ok(ApiResponse<BatchAssignResponse>.Success(
                ErrorCodes.SuccessBatchAssign,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}/members/batch-assign/template
        /// Download pre-filled CSV template for batch assignment
        /// Pre-fills Email and GroupName columns
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        [HttpGet("{studioId}/members/batch-assign/template")]
        public async Task<IActionResult> DownloadBatchAssignTemplate(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var template = await _batchAssignService.GenerateTemplateAsync(studioId, userId);

            return File(template, "text/csv", "batch_assign_template.csv");
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio/{studioId}/groups/random-assign
        /// Randomly assign studio members to groups
        /// Can assign to specific groups or all groups in studio
        /// </summary>
        /// <param name="studioId">Studio ID</param>
        /// <param name="request">Random assign parameters</param>
        /// <param name="cancellationToken">Cancellation token</param>
        [HttpPost("{studioId}/groups/random-assign")]
        public async Task<ActionResult<ApiResponse<RandomAssignResponse>>> RandomAssign(
            Guid studioId,
            [FromBody] RandomAssignRequest request,
            CancellationToken cancellationToken)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _batchAssignService.RandomAssignAsync(
                studioId,
                userId,
                request,
                cancellationToken);

            var message = _messageService.GetMessage(ErrorCodes.SuccessRandomAssign);
            return Ok(ApiResponse<RandomAssignResponse>.Success(
                ErrorCodes.SuccessRandomAssign,
                message,
                result));
        }

        // Toggle IsOpen setting (Owner only)
        /// <summary>
        /// [AUTHORIZED] PUT /api/studio/{studioId}/toggle-open
        /// Toggle the IsOpen setting of a studio (open vs closed membership)
        /// Validate: User must be Owner of studio
        /// </summary>
        [HttpPut("{studioId}/toggle-open")]
        public async Task<ActionResult<ApiResponse<ToggleIsOpenResponse>>> ToggleIsOpen(
            Guid studioId,
            [FromBody] ToggleIsOpenRequest request)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.ToggleIsOpenAsync(userId, studioId, request.IsOpen);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateStudio);

            return Ok(ApiResponse<ToggleIsOpenResponse>.Success(
                ErrorCodes.SuccessUpdateStudio,
                message,
                result));
        }

        // Get pending members (Owner only)
        /// <summary>
        /// [AUTHORIZED] GET /api/studio/{studioId}/pending
        /// Get list of pending (not yet approved) members
        /// Validate: User must be Owner of studio
        /// </summary>
        [HttpGet("{studioId}/pending")]
        public async Task<ActionResult<ApiResponse<StudioPendingMemberListResponse>>> GetPendingMembers(Guid studioId)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.GetPendingMembersAsync(userId, studioId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<StudioPendingMemberListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        // Approve pending member (Owner only)
        /// <summary>
        /// [AUTHORIZED] POST /api/studio/{studioId}/approve
        /// Approve a pending member to join the studio
        /// Validate: User must be Owner of studio
        /// </summary>
        [HttpPost("{studioId}/approve")]
        public async Task<ActionResult<ApiResponse<ApproveMemberResponse>>> ApproveMember(
            Guid studioId,
            [FromBody] ApproveMemberRequest request)
        {
            var userId = JwtHelper.ValidateAndGetUserId(User);
            var result = await _studioService.ApproveMemberAsync(userId, studioId, request.UserId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateData);

            return Ok(ApiResponse<ApproveMemberResponse>.Success(
                ErrorCodes.SuccessUpdateData,
                message,
                result));
        }
    }
}
