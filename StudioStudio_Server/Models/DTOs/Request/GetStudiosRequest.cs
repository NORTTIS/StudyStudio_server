namespace StudioStudio_Server.Models.DTOs.Request
{
    public class GetStudiosRequest
    {
        /// <summary>
        /// Tìm kiếm theo tên studio
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Số trang (mặc định: 1)
        /// </summary>
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Kích thước trang (mặc định: 10)
        /// </summary>
        public int PageSize { get; set; } = 10;
    }
}
