using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Text.RegularExpressions;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service handling business logic for Reports
    /// </summary>
    public class ReportService(
        IReportRepository reportRepository,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<ReportService> logger) : IReportService
    {
        private readonly Regex _emailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

        /// <summary>
        /// Send report/feedback
        /// Validate: Email format
        /// Action:
        /// 1. Save report to database with Status = Open, Priority = Low (default)
        /// 2. Send email notification to admin
        /// Note: userId can be null if user is not logged in (public API)
        /// </summary>
        public async Task SendReportAsync(Guid? userId, ReportRequest request)
        {
            ValidateEmailFormat(request.Email);

            var reportToEmail = configuration["Report:ToEmail"];
            if (string.IsNullOrWhiteSpace(reportToEmail))
            {
                throw new AppException(
                    ErrorCodes.ReportEmailNotConfigured,
                    StatusCodes.Status500InternalServerError);
            }

            var report = new Report
            {
                ReportId = Guid.NewGuid(),
                UserId = userId,
                Email = request.Email,
                Title = request.Title,
                Content = request.Content,
                Type = request.Type,
                Status = ReportStatus.Open,
                Priority = ReportPriority.Low,
                CreatedAt = DateTime.UtcNow
            };

            await reportRepository.AddAsync(report);

            var subject = $"[Report] {request.Type} - {request.Title}";
            var body = EmailTemplate.ReportEmail(
                request.Type.ToString(),
                request.Title,
                request.Email,
                request.Content,
                userId?.ToString() ?? "Anonymous");

            await emailService.SendLinkAsync(reportToEmail, subject, body);

            logger.LogInformation(
                "Report sent. Type: {Type}, Email: {Email}, UserId: {UserId}",
                request.Type, request.Email, userId?.ToString() ?? "Anonymous");
        }

        /// <summary>
        /// Get reports with filtering, pagination and summary
        /// Admin only
        /// </summary>
        public async Task<ReportListResponse> GetReportsAsync(GetReportsRequest request)
        {
            var reports = await reportRepository.GetReportsAsync(
                request.SearchTerm,
                request.Type,
                request.Status,
                request.PageNumber,
                request.PageSize);

            var totalCount = await reportRepository.GetTotalReportsCountAsync(
                request.SearchTerm,
                request.Type,
                request.Status);

            var totalOpen = await reportRepository.GetReportsCountByStatusAsync(ReportStatus.Open);
            var totalInProgress = await reportRepository.GetReportsCountByStatusAsync(ReportStatus.InProgress);
            var totalResolved = await reportRepository.GetReportsCountByStatusAsync(ReportStatus.Resolved);
            var totalReport = totalOpen + totalInProgress + totalResolved;

            var reportList = reports.Select(r => new ReportItemResponse
            {
                ReportId = r.ReportId,
                Type = r.Type,
                Email = r.Email,
                Title = r.Title,
                Content = r.Content,
                Status = r.Status,
                Priority = r.Priority,
                AdminNote = r.AdminNote,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                UserId = r.UserId
            }).ToList();

            return new ReportListResponse
            {
                Summary = new ReportSummaryResponse
                {
                    TotalReport = totalReport,
                    TotalOpen = totalOpen,
                    TotalInProgress = totalInProgress,
                    TotalResolved = totalResolved
                },
                ReportList = reportList,
                TotalCount = totalCount,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize
            };
        }

        /// <summary>
        /// Update report (Admin only)
        /// Can update: Status, Priority, AdminNote
        /// Validate: Report must exist
        /// Auto-set: UpdatedAt = UtcNow
        /// </summary>
        public async Task<ReportItemResponse> UpdateReportAsync(Guid adminUserId, UpdateReportRequest request)
        {
            var report = await reportRepository.GetReportByIdAsync(request.ReportId);

            if (report == null)
            {
                throw new AppException(
                    ErrorCodes.ReportNotFound,
                    StatusCodes.Status404NotFound);
            }

            if (request.Status.HasValue)
            {
                report.Status = request.Status.Value;
            }

            if (request.Priority.HasValue)
            {
                report.Priority = request.Priority.Value;
            }

            if (request.AdminNote != null)
            {
                report.AdminNote = request.AdminNote;
            }

            report.UpdatedAt = DateTime.UtcNow;

            await reportRepository.UpdateAsync(report);

            logger.LogInformation(
                "Report updated by admin. ReportId: {ReportId}, AdminId: {AdminId}",
                report.ReportId, adminUserId);

            return new ReportItemResponse
            {
                ReportId = report.ReportId,
                Type = report.Type,
                Email = report.Email,
                Title = report.Title,
                Content = report.Content,
                Status = report.Status,
                Priority = report.Priority,
                AdminNote = report.AdminNote,
                CreatedAt = report.CreatedAt,
                UpdatedAt = report.UpdatedAt,
                UserId = report.UserId
            };
        }

        /// <summary>
        /// Validate email format
        /// </summary>
        private void ValidateEmailFormat(string email)
        {
            if (!_emailRegex.IsMatch(email))
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidEmail);
            }
        }
    }
}
