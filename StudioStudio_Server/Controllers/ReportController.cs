using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace StudioStudio_Server.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly StudioDbContext _db;
        private readonly IEmailService _emailService;
        private readonly IMessageService _messageService;
        private readonly IConfiguration _configuration;

        public ReportController(
            StudioDbContext db,
            IEmailService emailService,
            IMessageService messageService,
            IConfiguration configuration)
        {
            _db = db;
            _emailService = emailService;
            _messageService = messageService;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> SendReport([FromBody] ReportRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Type) ||
                string.IsNullOrWhiteSpace(request.Title) ||
                string.IsNullOrWhiteSpace(request.Content) ||
                string.IsNullOrWhiteSpace(request.Email))
            {
                throw new AppException(ErrorCodes.ReportInvalidRequest, StatusCodes.Status400BadRequest);
            }

            var reportToEmail = _configuration["Report:ToEmail"];
            if (string.IsNullOrWhiteSpace(reportToEmail))
            {
                throw new AppException(ErrorCodes.ReportEmailNotConfigured, StatusCodes.Status500InternalServerError);
            }

            var userId = GetUserIdOrEmpty();
            var reportContent = JsonSerializer.Serialize(new
            {
                request.Type,
                request.Email,
                request.Title,
                request.Content
            });

            var report = new Report
            {
                ReportId = Guid.NewGuid(),
                UserId = userId,
                Content = reportContent,
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Reports.Add(report);
            await _db.SaveChangesAsync();

            var subject = $"[Report] {request.Type} - {request.Title}";
            var body = $@"
                <h3>Report Type</h3>
                <p>{request.Type}</p>
                <h3>Title</h3>
                <p>{request.Title}</p>
                <h3>Email</h3>
                <p>{request.Email}</p>
                <h3>Content</h3>
                <p>{request.Content}</p>
                <h3>UserId</h3>
                <p>{userId}</p>
            ";

            await _emailService.SendLinkAsync(reportToEmail, subject, body);

            var message = _messageService.GetMessage(ErrorCodes.SuccessReportSent);
            return Ok(ApiResponse<object>.Success(message));
        }

        private Guid GetUserIdOrEmpty()
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(ClaimTypes.Name)?.Value
                ?? User.FindFirst(ClaimTypes.Email)?.Value;

            return Guid.TryParse(userIdValue, out var userId) ? userId : Guid.Empty;
        }
    }
}
