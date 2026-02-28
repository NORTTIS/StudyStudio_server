using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? business logic cho Reports
    /// </summary>
    public class ReportService : IReportService
    {
        private readonly IReportRepository _reportRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ReportService> _logger;
        private readonly Regex _emailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        public ReportService(
            IReportRepository reportRepository,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<ReportService> logger)
        {
            _reportRepository = reportRepository;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// G?i report/feedback
        /// Validate: Email format
        /// Action:
        /// 1. Lýu report vào database v?i Status = Pending
        /// 2. G?i email thông báo t?i admin
        /// Note: userId có th? null n?u user chýa ðãng nh?p (public API)
        /// </summary>
        public async Task SendReportAsync(Guid? userId, ReportRequest request)
        {
            ValidateEmailFormat(request.Email);

            var reportToEmail = _configuration["Report:ToEmail"];
            if (string.IsNullOrWhiteSpace(reportToEmail))
            {
                throw new AppException(
                    ErrorCodes.ReportEmailNotConfigured,
                    StatusCodes.Status500InternalServerError);
            }

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
                UserId = userId ?? Guid.Empty,
                Content = reportContent,
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            await _reportRepository.AddAsync(report);

            var subject = $"[Report] {request.Type} - {request.Title}";
            var body = EmailTemplate.ReportEmail(
                request.Type,
                request.Title,
                request.Email,
                request.Content,
                userId?.ToString() ?? "Anonymous");

            await _emailService.SendLinkAsync(reportToEmail, subject, body);

            _logger.LogInformation(
                "Report sent. Type: {Type}, Email: {Email}, UserId: {UserId}",
                request.Type, request.Email, userId?.ToString() ?? "Anonymous");
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        private void ValidateEmailFormat(string email)
        {
            if (!_emailRegex.IsMatch(email))
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidEmail,
                    StatusCodes.Status400BadRequest);
            }
        }
    }
}
