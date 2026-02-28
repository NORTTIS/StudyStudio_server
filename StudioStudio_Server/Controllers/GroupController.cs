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
    /// Controller quản lý Groups (nhóm học tập)
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
        /// Xác thực và lấy userId từ JWT token
        /// Validate: User không được là admin (admin không dùng user APIs)
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
        /// Lấy danh sách tất cả groups của user
        /// Bao gồm: Favorites, Studio Groups, Independent Groups
        /// Sắp xếp: Theo category và UpdatedAt DESC
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
        /// Lấy chi tiết một group
        /// Validate: User phải là member của group
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
        /// [AUTHORIZED] GET /api/group/{groupId}/members
        /// Lấy danh sách members trong group
        /// Validate: User phải là member của group
        /// Sắp xếp: Owner → Moderator → Member, sau đó theo JoinedAt ASC
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
        /// Tạo mới một group (Studio hoặc Independent)
        /// Validate:
        /// - Group limit theo subscription plan
        /// - Group name không trùng trong cùng studio (nếu có)
        /// - User phải là owner của studio (nếu tạo trong studio)
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
        /// Tạo nhiều groups cùng lúc trong một studio (batch create)
        /// Use case: Giáo viên tạo nhiều nhóm lớp học cùng lúc
        /// Validate:
        /// - User phải là owner của studio
        /// - Tổng số groups không vượt quá limit
        /// - Group names không trùng lặp
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
        /// Cập nhật thông tin group
        /// Validate:
        /// - User phải là Owner hoặc Moderator
        /// - Group name không trùng (nếu đổi tên)
        /// - Template chỉ user-created groups mới có thể set
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
        /// Xóa (soft delete) một group
        /// Validate: User phải là Owner của group
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
