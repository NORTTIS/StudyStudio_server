using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Reports (báo cáo/feedback)
    /// Route: /api/reports
    /// Note: Public API - không c?n authentication
    /// </summary>
    [Route("api/reports")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly IMessageService _messageService;

        public ReportController(
            IReportService reportService,
            IMessageService messageService)
        {
            _reportService = reportService;
            _messageService = messageService;
        }

        /// <summary>
        /// L?y userId t? claims (nullable - public API)
        /// Return: userId n?u user ð? ðãng nh?p, null n?u anonymous
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
        /// G?i báo cáo/feedback (bug report, feature request, etc.)
        /// Validate: Email format
        /// Action:
        /// 1. Lýu report vào database (Status = Pending)
        /// 2. G?i email notification t?i admin
        /// Note: Không c?n authentication - anonymous users có th? g?i
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendReport([FromBody] ReportRequest request)
        {
            var userId = GetUserIdOrNull();
            await _reportService.SendReportAsync(userId, request);
            var message = _messageService.GetMessage(ErrorCodes.SuccessReportSent);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessReportSent,
                message,
                null));
        }
    }
}
