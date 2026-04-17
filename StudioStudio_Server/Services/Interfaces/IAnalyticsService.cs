using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface IAnalyticsService
    {
        /// Lấy tóm tắt nhóm (tổng quan công việc, hoạt động, đóng góp thành viên)
        Task<GroupSummaryResponse> GetGroupSummaryAsync(Guid groupId, Guid userId);

        /// Lấy xu hướng tiến độ thành viên theo ngày
        Task<List<MemberProgressTrendData>> GetMemberProgressTrendAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate, List<Guid>? memberIds = null);

        /// Lấy bản đồ nhiệt hoạt động của thành viên
        Task<List<MemberHeatmapData>> GetMemberHeatmapAsync(Guid groupId, DateOnly? startDate, DateOnly? endDate);

        /// Lấy tổng quan studio (tiến độ nhóm, trạng thái công việc)
        Task<StudioOverviewResponse> GetStudioOverviewAsync(Guid studioId);

        /// Lấy xu hướng hoàn thành theo nhóm theo thời gian
        Task<StudioCompletionTrendResponse> GetStudioCompletionTrendAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate,
            List<Guid>? groupIds);

        /// Lấy hoạt động nhóm theo ngày (bản đồ nhiệt)
        Task<StudioGroupActivityResponse> GetStudioGroupActivityAsync(
            Guid studioId,
            DateOnly? startDate,
            DateOnly? endDate);

        /// Lấy tóm tắt KPI người dùng (tổng công việc, hoàn thành, quá hạn)
        Task<UserKpiSummaryResponse> GetUserKpiSummaryAsync(Guid userId);

        /// Lấy trạng thái công việc người dùng (donut chart)
        Task<UserTaskStatusResponse> GetUserTaskStatusAsync(Guid userId);

        /// Lấy xếp hạng nhóm của người dùng
        Task<UserGroupRankingsResponse> GetUserGroupRankingsAsync(Guid userId);

        /// Lấy xu hướng năng suất người dùng (area chart)
        Task<UserProductivityTrendResponse> GetUserProductivityTrendAsync(Guid userId, int periodDays = 30);

        /// Lấy phân bổ công việc theo mức ưu tiên
        Task<UserPriorityDistributionResponse> GetUserPriorityDistributionAsync(Guid userId);

        /// Lấy phân bổ công việc theo mức độ khẩn cấp
        Task<UserUrgencyDistributionResponse> GetUserUrgencyDistributionAsync(Guid userId);

        /// Lấy benchmark hiệu suất người dùng so với nhóm
        Task<UserBenchmarkResponse> GetUserBenchmarkAsync(Guid userId, int weeks = 7, Guid? groupId = null);

        /// Lấy cảnh báo rủi ro (quá hạn, sắp đến hạn)
        Task<UserRiskAlertsResponse> GetUserRiskAlertsAsync(Guid userId, int limit = 10);
    }
}