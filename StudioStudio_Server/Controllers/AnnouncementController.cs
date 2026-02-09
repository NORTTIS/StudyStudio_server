using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;

namespace StudioStudio_Server.Controllers
{
    [Route("api/announcements")]
    [ApiController]
    public class AnnouncementController : ControllerBase
    {
        private readonly StudioDbContext _db;

        public AnnouncementController(StudioDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAnnouncements()
        {
            // Mock data - later can be replaced with real database query
            var mockAnnouncements = new List<AnnouncementResponse>
            {
                new AnnouncementResponse
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "Chào mừng đến với Study Studio!",
                    Content = "Study Studio giúp bạn quản lý công việc và học tập hiệu quả hơn. Hãy khám phá các tính năng mới!",
                    Type = AnnouncementType.Info.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    PublishedAt = DateTime.UtcNow.AddDays(-7)
                },
                new AnnouncementResponse
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "Bảo trì hệ thống",
                    Content = "Hệ thống sẽ bảo trì vào 2h sáng ngày mai. Thời gian dự kiến: 1 giờ.",
                    Type = AnnouncementType.Maintenance.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    PublishedAt = DateTime.UtcNow.AddDays(-2)
                },
                new AnnouncementResponse
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "Tính năng mới: Tích hợp AI",
                    Content = "Chúng tôi đã thêm tính năng AI để gợi ý công việc và lên lịch thông minh.",
                    Type = AnnouncementType.Info.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    PublishedAt = DateTime.UtcNow.AddDays(-1)
                },
                new AnnouncementResponse
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "Lưu ý bảo mật",
                    Content = "Vui lòng không chia sẻ mật khẩu của bạn với bất kỳ ai. Thường xuyên thay đổi mật khẩu để đảm bảo an toàn.",
                    Type = AnnouncementType.Warning.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    PublishedAt = DateTime.UtcNow.AddHours(-12)
                },
                new AnnouncementResponse
                {
                    AnnouncementId = Guid.NewGuid(),
                    Title = "Giới thiệu chương trình khuyến mãi",
                    Content = "Đăng ký gói Premium ngay hôm nay để nhận ưu đãi 50% trong tháng đầu tiên!",
                    Type = AnnouncementType.Info.ToString(),
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow.AddHours(-6),
                    PublishedAt = DateTime.UtcNow.AddHours(-6)
                }
            };

            // Sort by published date descending
            var sortedAnnouncements = mockAnnouncements
                .OrderByDescending(a => a.PublishedAt)
                .ToList();

            return Ok(ApiResponse<List<AnnouncementResponse>>.Success(
                "Announcements retrieved successfully",
                sortedAnnouncements
            ));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAnnouncementById(Guid id)
        {
            var announcement = await _db.Announcements
                .Where(a => a.AnnouncementId == id && a.IsActive)
                .Select(a => new AnnouncementResponse
                {
                    AnnouncementId = a.AnnouncementId,
                    Title = a.Title,
                    Content = a.Content,
                    Type = a.Type.ToString(),
                    IsActive = a.IsActive,
                    CreatedAt = a.CreatedAt,
                    PublishedAt = a.PublishedAt
                })
                .FirstOrDefaultAsync();

            if (announcement == null)
            {
                return NotFound(ApiResponse<object>.Error("Announcement not found"));
            }

            return Ok(ApiResponse<AnnouncementResponse>.Success(
                "Announcement retrieved successfully",
                announcement
            ));
        }
    }
}
