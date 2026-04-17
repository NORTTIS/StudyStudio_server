using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Reports (reports/feedback)
    /// Route: /api/reports
    /// Note: Public API - authentication not required
    /// </summary>
    [Route("api/reports")]
    [ApiController]
    public class ReportController(
        IReportService reportService,
        IMessageService messageService) : ControllerBase
    {
        /// <summary>
        /// Get userId from claims (nullable - public API)
        /// Return: userId if user is logged in, null if anonymous
        /// </summary>
        private Guid? GetUserIdOrNull()
        {
            var userIdValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? User.FindFirst(ClaimTypes.Name)?.Value
                              ?? User.FindFirst(ClaimTypes.Email)?.Value;

            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }

        /// <summary>
        /// [PUBLIC] POST /api/reports
        /// Send report/feedback (bug report, feature request, etc.)
        /// Validate: Email format
        /// Action:
        /// 1. Save report to database (Status = Pending)
        /// 2. Send email notification to admin
        /// Note: Authentication not required - anonymous users can send
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendReport([FromBody] ReportRequest request)
        {
            var userId = GetUserIdOrNull();
            await reportService.SendReportAsync(userId, request);
            var message = messageService.GetMessage(ErrorCodes.SuccessReportSent);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessReportSent,
                message));
        }
    }
}
