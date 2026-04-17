using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/analytics")]
    [ApiController]
    [Authorize]
    public class AnalyticsController(IAnalyticsService analyticsService) : ControllerBase
    {
        /// Xác thực người dùng và lấy userId từ JWT token.
        /// Kiểm tra: Người dùng không được là admin (admin không thể sử dụng API người dùng).
        /// Trả về: UserId của người dùng hiện tại
        /// lỗi: Khi token không hợp lệ hoặc người dùng là admin
        private Guid ValidateAndGetUserId()
        {
            // Lấy claim NameIdentifier từ JWT token
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Kiểm tra claim có tồn tại và có định dạng GUID hợp lệ hay không
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                // Token không hợp lệ hoặc không chứa userId -> trả về lỗi 401
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            // Kiểm tra xem người dùng có phải là admin hay không
            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            // Parse giá trị claim IsAdmin thành boolean
            // Chỉ cần một trong các điều kiện thất bại (claim null, parse thất bại) thì isAdmin = false
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            // Nếu người dùng là admin -> từ chối truy cập với lỗi 403
            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        // GROUP ANALYTICS
        /// Lấy tóm tắt nhóm (toàn bộ thời gian, không lọc theo ngày).
        /// Trả về: phân tích công việc, tóm tắt hoạt động và dữ liệu đóng góp.
        /// Tham số: ID của nhóm cần lấy thông tin tóm tắt
        /// Tóm tắt nhóm với các thông tin tổng quan
        [HttpGet("group/{groupId}/summary")]
        public async Task<ActionResult<ApiResponse<GroupSummaryResponse>>> GetGroupSummary(
            Guid groupId)
        {
            var userId = ValidateAndGetUserId();

            var result = await analyticsService.GetGroupSummaryAsync(groupId, userId);

            return Ok(ApiResponse<GroupSummaryResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }

        /// Lấy xu hướng tiến độ của thành viên với bộ lọc ngày và bộ lọc thành viên tùy chọn.
        /// Tham số: ID của nhóm, ngày bắt đầu, ngày kết thúc, danh sách ID thành viên cần lọc
        /// Trả về: Danh sách dữ liệu xu hướng tiến độ của từng thành viên
        [HttpGet("group/{groupId}/trend")]
        public async Task<ActionResult<ApiResponse<List<MemberProgressTrendData>>>> GetMemberProgressTrend(
            Guid groupId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] List<Guid>? memberIds = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await analyticsService.GetMemberProgressTrendAsync(groupId, start, end, memberIds);

            return Ok(ApiResponse<List<MemberProgressTrendData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }

        /// Lấy bản đồ nhiệt (heatmap) hoạt động của các thành viên với bộ lọc ngày.
        /// Tham số: ID của nhóm, ngày bắt đầu, ngày kết thúc
        /// Trả về: Danh sách dữ liệu heatmap cho từng thành viên theo ngày
        [HttpGet("group/{groupId}/heatmap")]
        public async Task<ActionResult<ApiResponse<List<MemberHeatmapData>>>> GetMemberHeatmap(
            Guid groupId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await analyticsService.GetMemberHeatmapAsync(groupId, start, end);

            return Ok(ApiResponse<List<MemberHeatmapData>>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }



        // ==================== STUDIO OVERVIEW (Chart 1 & 2) ====================
        /// Lấy tổng quan studio với tóm tắt tất cả các nhóm (không lọc theo ngày).
        /// Sử dụng cho Chart 1 (Tiến độ nhóm) và Chart 2 (Trạng thái công việc theo nhóm).
        /// Tham số: ID của studio
        /// Trả về: Tổng quan studio với dữ liệu biểu đồ
        [HttpGet("studio/{studioId}/overview")]
        public async Task<ActionResult<ApiResponse<StudioOverviewResponse>>> GetStudioOverview(
            Guid studioId)
        {
            var userId = ValidateAndGetUserId();

            var result = await analyticsService.GetStudioOverviewAsync(studioId);

            return Ok(ApiResponse<StudioOverviewResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }

        // ==================== STUDIO COMPLETION TREND (Chart 3) ====================
        /// Lấy xu hướng hoàn thành task theo nhóm có bộ lọc ngày.
        /// Sử dụng cho Chart 3 (Biểu đồ đường).
        /// Tham số: ID của studio, ngày bắt đầu, ngày kết thúc, danh sách ID nhóm
        /// Trả về: Dữ liệu xu hướng hoàn thành theo thời gian
        [HttpGet("studio/{studioId}/completion-trend")]
        public async Task<ActionResult<ApiResponse<StudioCompletionTrendResponse>>> GetStudioCompletionTrend(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? groupIds = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            List<Guid>? parsedGroupIds = null;
            if (!string.IsNullOrWhiteSpace(groupIds))
            {
                // frontend sẽ truyền lên danh sách groupId dưới dạng chuỗi phân tách bằng dấu phẩy, ví dụ: "guid1,guid2,guid3"
                // Tách chuỗi theo dấu phẩy, loại bỏ khoảng trắng thừa
                // TryParse để lọc bỏ các chuỗi không phải GUID hợp lệ
                // Where(g => g.HasValue) để loại bỏ các giá trị null từ parse thất bại
                // Select(g => g!.Value) để chuyển từ Guid? sang Guid
                parsedGroupIds = groupIds
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Guid.TryParse(s.Trim(), out var g) ? g : (Guid?)null)
                    .Where(g => g.HasValue)
                    .Select(g => g!.Value)
                    .ToList();
            }

            var result = await analyticsService.GetStudioCompletionTrendAsync(studioId, start, end, parsedGroupIds);

            return Ok(ApiResponse<StudioCompletionTrendResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }


        // ==================== STUDIO GROUP ACTIVITY (Chart 5) ====================

        /// Lấy dữ liệu bản đồ nhiệt hoạt động theo nhóm có hỗ trợ bộ lọc ngày.
        /// Mức độ hoạt động chia làm 4 level
        /// Sử dụng cho Chart 5 (Bản đồ nhiệt hoạt động).
        ///
        /// Công thức tính Activity Score:
        ///   Score = tasksCompleted×4 + tasksCreated×3 + tasksUpdated×2 + commentsCreated×1 + messagesSent×1
        ///
        /// Ngưỡng Activity Level:
        ///   0 = 0 điểm (Không hoạt động)
        ///   1 = 1-5 điểm (Hoạt động thấp)
        ///   2 = 6-15 điểm (Hoạt động trung bình)
        ///   3 = 16-30 điểm (Hoạt động cao)
        ///   4 = 31+ điểm (Rất hoạt động)
        /// Tham số: ID của studio, ngày bắt đầu, ngày kết thúc
        /// Trả về: Dữ liệu hoạt động theo nhóm và ngày
        [HttpGet("studio/{studioId}/group-activity")]
        public async Task<ActionResult<ApiResponse<StudioGroupActivityResponse>>> GetStudioGroupActivity(
            Guid studioId,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = ValidateAndGetUserId();

            DateOnly? start = startDate.HasValue ? DateOnly.FromDateTime(startDate.Value) : null;
            DateOnly? end = endDate.HasValue ? DateOnly.FromDateTime(endDate.Value) : null;

            var result = await analyticsService.GetStudioGroupActivityAsync(studioId, start, end);

            return Ok(ApiResponse<StudioGroupActivityResponse>.Success(
                ErrorCodes.SuccessGetData,
                "Data retrieved successfully",
                result));
        }

        // ==================== PERSONAL ANALYTICS (AnalysisHome) ====================
        /// Lấy tóm tắt KPI của người dùng.
        /// Bao gồm: tổng công việc, đã hoàn thành, quá hạn, tỷ lệ hoàn thành, thời gian trung bình.
        /// Tham số: ID của người dùng cần lấy KPI
        /// Trả về: Tóm tắt KPI của người dùng
        [HttpGet("user/{userId}/kpi-summary")]
        public async Task<ActionResult<ApiResponse<UserKpiSummaryResponse>>> GetUserKpiSummary(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();

            // Kiểm tra người dùng hiện tại chỉ có thể xem KPI của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserKpiSummaryAsync(userId);
            return Ok(ApiResponse<UserKpiSummaryResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy trạng thái công việc của người dùng dưới dạng donut chart.
        /// Phân loại: Hoàn thành, Đang làm, Chưa bắt đầu, Quá hạn.
        /// Tham số: ID của người dùng
        /// Trả về: Phân bổ công việc theo trạng thái
        [HttpGet("user/{userId}/task-status")]
        public async Task<ActionResult<ApiResponse<UserTaskStatusResponse>>> GetUserTaskStatus(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem trạng thái công việc của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserTaskStatusAsync(userId);
            return Ok(ApiResponse<UserTaskStatusResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy xếp hạng nhóm của người dùng qua các studio khác nhau, sắp xếp theo điểm số.
        /// Tham số: ID của người dùng
        /// Trả về: Danh sách xếp hạng nhóm của người dùng
        [HttpGet("user/{userId}/group-rankings")]
        public async Task<ActionResult<ApiResponse<UserGroupRankingsResponse>>> GetUserGroupRankings(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem xếp hạng của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserGroupRankingsAsync(userId);
            return Ok(ApiResponse<UserGroupRankingsResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy xu hướng năng suất của người dùng trong N ngày gần nhất (biểu đồ area).
        /// Tham số: ID của người dùng, số ngày cần lấy dữ liệu (mặc định: 30 ngày, tối đa: 365 ngày)
        /// Trả về:Dữ liệu xu hướng năng suất theo thời gian
        [HttpGet("user/{userId}/productivity-trend")]
        public async Task<ActionResult<ApiResponse<UserProductivityTrendResponse>>> GetUserProductivityTrend(
            Guid userId,
            [FromQuery] int period = 30)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem năng suất của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserProductivityTrendAsync(userId, period);
            return Ok(ApiResponse<UserProductivityTrendResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        
        /// Lấy phân bổ công việc theo mức ưu tiên.
        /// Phân loại: Cao, Trung bình, Thấp.
        /// Tham số: ID của người dùng
        /// Trả về: Phân bổ công việc theo mức ưu tiên
        [HttpGet("user/{userId}/priority-distribution")]
        public async Task<ActionResult<ApiResponse<UserPriorityDistributionResponse>>> GetUserPriorityDistribution(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem phân bổ của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserPriorityDistributionAsync(userId);
            return Ok(ApiResponse<UserPriorityDistributionResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy phân bổ công việc theo mức độ khẩn cấp.
        /// Phân loại: Khẩn cấp, Cao, Trung bình, Thấp.
        /// Tham số: ID của người dùng
        /// Trả về: Phân bổ công việc theo mức độ khẩn cấp
        [HttpGet("user/{userId}/urgency-distribution")]
        public async Task<ActionResult<ApiResponse<UserUrgencyDistributionResponse>>> GetUserUrgencyDistribution(Guid userId)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem phân bổ của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserUrgencyDistributionAsync(userId);
            return Ok(ApiResponse<UserUrgencyDistributionResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy điểm chuẩn hiệu suất hàng tuần của người dùng so với trung bình nhóm.
        /// Tham số: ID của người dùng
        /// Trả về: Dữ liệu benchmark hiệu suất
        [HttpGet("user/{userId}/benchmark")]
        public async Task<ActionResult<ApiResponse<UserBenchmarkResponse>>> GetUserBenchmark(
            Guid userId,
            [FromQuery] int weeks = 7,
            [FromQuery] Guid? groupId = null)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem benchmark của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserBenchmarkAsync(userId, weeks, groupId);
            return Ok(ApiResponse<UserBenchmarkResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }

        /// Lấy các cảnh báo rủi ro của người dùng.
        /// Bao gồm: công việc quá hạn, công việc sắp đến hạn, công việc bị kẹt.
        /// Tham số: ID của người dùng
        /// Trả về: Danh sách cảnh báo rủi ro được sắp xếp theo mức độ ưu tiên
        [HttpGet("user/{userId}/risk-alerts")]
        public async Task<ActionResult<ApiResponse<UserRiskAlertsResponse>>> GetUserRiskAlerts(
            Guid userId,
            [FromQuery] int limit = 10)
        {
            var currentUserId = ValidateAndGetUserId();

            // Chỉ cho phép người dùng xem cảnh báo của chính mình
            if (currentUserId != userId)
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);

            var result = await analyticsService.GetUserRiskAlertsAsync(userId, limit);
            return Ok(ApiResponse<UserRiskAlertsResponse>.Success(
                ErrorCodes.SuccessGetData, "Data retrieved successfully", result));
        }
    }
}
