using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class HomeController : ControllerBase
    {
        private readonly IHomeService _homeService;
        private readonly IMessageService _messageService;

        public HomeController(
            IHomeService homeService,
            IMessageService messageService)
        {
            _homeService = homeService;
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
        /// Get summary metrics for Home screen.
        /// </summary>
        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<HomeSummaryResponse>>> GetSummary()
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.GetHomeSummaryAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<HomeSummaryResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get merged task list for Home screen with pagination.
        /// Includes personal tasks and assigned group tasks.
        /// </summary>
        [HttpGet("TaskList")]
        public async Task<ActionResult<ApiResponse<HomeTaskListResponse>>> GetTaskList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.GetHomeTaskListAsync(userId, page, pageSize);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<HomeTaskListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Get personal task board with statuses and tasks.
        /// Returns: List of PersonalTaskStatus, each containing its tasks.
        /// </summary>
        [HttpGet("personal-task")]
        public async Task<ActionResult<ApiResponse<PersonalTaskBoardResponse>>> GetPersonalTaskBoard()
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.GetPersonalTaskBoardAsync(userId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<PersonalTaskBoardResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// Create a new personal task status.
        /// </summary>
        [HttpPost("personal-status")]
        public async Task<ActionResult<ApiResponse<PersonalTaskStatusResponse>>> CreatePersonalTaskStatus(
            [FromBody] PersonalTaskStatusRequest request)
        {
            var userId = ValidateAndGetUserId();
            var result = await _homeService.CreateNewPersonalTaskStatus(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateTaskStatus);

            return Ok(ApiResponse<PersonalTaskStatusResponse>.Success(
                ErrorCodes.SuccessCreateTaskStatus,
                message,
                result));
        }

        /// <summary>
        /// Reorder personal task status columns.
        /// </summary>
        [HttpPut("personal-status/reorder")]
        public async Task<ActionResult<ApiResponse<object>>> ReorderTaskStatus(
            [FromBody] ReorderPersonalTaskStatusRequest request)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.ReorderPersonalTaskStatus(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        /// <summary>
        /// Update personal task status details.
        /// </summary>
        [HttpPut("personal-status/{statusId}/update-detail")]
        public async Task<ActionResult<ApiResponse<object>>> UpdateTaskStatusDetail(
            [FromBody] PersonalTaskStatusRequest request, Guid statusId)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.UpdatePersonalTaskStatus(userId, statusId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessUpdateTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessUpdateTaskStatus,
                message,
                null));
        }

        /// <summary>
        /// Delete a personal task status.
        /// </summary>
        [HttpDelete("personal-status/{statusId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeletePersonalTaskStatus(Guid statusId)
        {
            var userId = ValidateAndGetUserId();
            await _homeService.DeletePersonalTaskStatus(userId, statusId);
            var message = _messageService.GetMessage(ErrorCodes.SuccessDeleteTaskStatus);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteTaskStatus,
                message,
                null));
        }
    }
}
