using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Utils;

namespace StudioStudio_Server.Controllers.Admin
{
    /// <summary>
    /// Admin Controller for managing Reports
    /// Route: /api/admin/reports
    /// </summary>
    [Route("api/admin/reports")]
    [ApiController]
    [Authorize]
    public class AdminReportController(
        IReportService reportService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// [ADMIN] GET /api/admin/reports
        /// Get all reports with filtering, pagination and summary
        /// Query params:
        /// - searchTerm: Search by title or email
        /// - type: Filter by report type
        /// - status: Filter by report status
        /// - pageNumber: Page number (default 1)
        /// - pageSize: Page size (default 10)
        /// Response includes:
        /// - Summary: TotalReport, TotalOpen, TotalInProgress, TotalResolved
        /// - ReportList: Paginated list of reports
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<ReportListResponse>>> GetReports(
            [FromQuery] GetReportsRequest request)
        {
            JwtHelper.ValidateAdminUser(User);

            var response = await reportService.GetReportsAsync(request);
            var message = messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<ReportListResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                response));
        }

        /// <summary>
        /// [ADMIN] PUT /api/admin/reports
        /// Update report (Status, Priority, AdminNote)
        /// Validate: Report must exist
        /// Only admin can update reports
        /// </summary>
        [HttpPut]
        public async Task<ActionResult<ApiResponse<ReportItemResponse>>> UpdateReport(
            [FromBody] UpdateReportRequest request)
        {
            var adminUserId = JwtHelper.ValidateAdminUser(User);

            var response = await reportService.UpdateReportAsync(adminUserId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessUpdateReport);

            return Ok(ApiResponse<ReportItemResponse>.Success(
                ErrorCodes.SuccessUpdateReport,
                message,
                response));
        }
    }
}
