namespace StudioStudio_Server.Models.DTOs.Response
{
    /// <summary>
    /// Item trong danh sách nhóm cho admin
    /// </summary>
    public class GroupListItem
    {
        /// <summary>
        /// Mã nhóm = Guid
        /// </summary>
        public Guid GroupId { get; set; }

        /// <summary>
        /// Tên nhóm
        /// </summary>
        public string GroupName { get; set; } = null!;

        /// <summary>
        /// Loại nhóm: "Độc lập" hoặc "Thuộc studio"
        /// </summary>
        public string GroupType { get; set; } = null!;

        /// <summary>
        /// Tên studio nếu nhóm thuộc studio
        /// </summary>
        public string? StudioName { get; set; }

        /// <summary>
        /// Số thành viên
        /// </summary>
        public int MemberCount { get; set; }

        /// <summary>
        /// Số công việc
        /// </summary>
        public int TaskCount { get; set; }

        /// <summary>
        /// Ngày tạo
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Hoạt động cuối (null thì hiển thị "-")
        /// </summary>
        public DateTime? LastActivityAt { get; set; }

        /// <summary>
        /// Trạng thái hoạt động
        /// </summary>
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Tổng kết danh sách nhóm
    /// </summary>
    public class GroupListSummary
    {
        /// <summary>
        /// Tổng số nhóm
        /// </summary>
        public int TotalGroups { get; set; }

        /// <summary>
        /// Số nhóm thuộc studio
        /// </summary>
        public int StudioGroups { get; set; }

        /// <summary>
        /// Số nhóm độc lập
        /// </summary>
        public int IndependentGroups { get; set; }

        /// <summary>
        /// Số nhóm đang hoạt động
        /// </summary>
        public int ActiveGroups { get; set; }

        /// <summary>
        /// Số nhóm không hoạt động
        /// </summary>
        public int InactiveGroups { get; set; }
    }

    /// <summary>
    /// Response chính cho API danh sách nhóm (Admin)
    /// </summary>
    public class AdminGroupListResponse
    {
        /// <summary>
        /// Tổng kết dữ liệu
        /// </summary>
        public GroupListSummary Summary { get; set; } = null!;

        /// <summary>
        /// Danh sách nhóm
        /// </summary>
        public List<GroupListItem> GroupList { get; set; } = new();

        /// <summary>
        /// Số trang hiện tại
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Kích thước trang
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Tổng số bản ghi
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Tổng số trang
        /// </summary>
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
