using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for Document Upload & Processing
    /// Hybrid flow: B2 Direct Upload + Backend processing
    /// Route: /api/documents
    /// </summary>
    [Route("api/documents")]
    [ApiController]
    [Authorize]
    public class DocumentController : ControllerBase
    {
        private readonly IDocumentService _documentService;
        private readonly IMessageService _messageService;

        public DocumentController(
            IDocumentService documentService,
            IMessageService messageService)
        {
            _documentService = documentService;
            _messageService = messageService;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin (admin cannot use user APIs)
        /// </summary>
        private Guid ValidateAndGetUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(
                    ErrorCodes.AuthInvalidCredential,
                    StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null &&
                          bool.TryParse(isAdminClaim, out var adminResult) &&
                          adminResult;

            if (isAdmin)
            {
                throw new AppException(
                    ErrorCodes.AuthForbidden,
                    StatusCodes.Status403Forbidden);
            }

            return userId;
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/documents/request-upload
        /// Step 1: Request upload permission
        /// 
        /// Backend generates upload URL and creates attachment record
        /// Status: Uploading
        /// </summary>
        [HttpPost("request-upload")]
        public async Task<ActionResult<ApiResponse<RequestDocumentUploadResponse>>> RequestUpload(
            [FromBody] RequestDocumentUploadRequest request)
        {
            Guid userId = ValidateAndGetUserId();

            RequestDocumentUploadResponse result = await _documentService.RequestUploadAsync(
                userId,
                request);

            string message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<RequestDocumentUploadResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/documents/{attachmentId}/complete
        /// Step 3: Complete upload
        /// 
        /// Frontend calls this after successfully uploading file to B2
        /// Backend will verify file exists, update status and start background processing
        /// </summary>
        [HttpPost("{attachmentId}/complete")]
        public async Task<ActionResult<ApiResponse<object>>> CompleteUpload(Guid attachmentId)
        {
            Guid userId = ValidateAndGetUserId();

            await _documentService.CompleteUploadAsync(userId, attachmentId);

            string message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessGetData,
                message,
                null));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/documents/{attachmentId}/status
        /// Get processing status of document
        /// 
        /// Status: Uploading, Processing, Completed, Failed
        /// </summary>
        [HttpGet("{attachmentId}/status")]
        public async Task<ActionResult<ApiResponse<DocumentStatusResponse>>> GetDocumentStatus(
            Guid attachmentId)
        {
            Guid userId = ValidateAndGetUserId();

            DocumentStatusResponse result = await _documentService.GetDocumentStatusAsync(
                userId,
                attachmentId);

            string message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<DocumentStatusResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] GET /api/documents/group/{groupId}
        /// Get list of all documents in group
        /// 
        /// Include: FileName, Status, ChunkCount, Uploader info, Created date
        /// </summary>
        [HttpGet("group/{groupId}")]
        public async Task<ActionResult<ApiResponse<GroupDocumentsResponse>>> GetGroupDocuments(
            Guid groupId)
        {
            Guid userId = ValidateAndGetUserId();

            GroupDocumentsResponse result = await _documentService.GetGroupDocumentsAsync(
                userId,
                groupId);

            string message = _messageService.GetMessage(ErrorCodes.SuccessGetData);

            return Ok(ApiResponse<GroupDocumentsResponse>.Success(
                ErrorCodes.SuccessGetData,
                message,
                result));
        }

        /// <summary>
        /// [AUTHORIZED] DELETE /api/documents/{attachmentId}
        /// Delete document (soft delete)
        /// 
        /// Validate: User must be uploader or member of group
        /// Effect:
        /// - Set IsDeleted = true
        /// - Delete vectors from Qdrant
        /// </summary>
        [HttpDelete("{attachmentId}")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteDocument(Guid attachmentId)
        {
            Guid userId = ValidateAndGetUserId();

            await _documentService.DeleteDocumentAsync(userId, attachmentId);

            string message = _messageService.GetMessage(ErrorCodes.SuccessDeleteGroup);

            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessDeleteGroup,
                message,
                null));
        }
    }
}
