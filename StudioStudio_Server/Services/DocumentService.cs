using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service x? l? document upload, processing và embedding
    /// Flow: Request Upload ? Upload to B2 ? Complete ? Background Processing ? Qdrant
    /// </summary>
    public class DocumentService : IDocumentService
    {
        private readonly IGroupAttachmentRepository _attachmentRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IFileStorageService _fileStorageService;
        private readonly IVectorDatabaseService _vectorDbService;
        private readonly IEmbeddingService _embeddingService;
        private readonly IUserRepository _userRepository;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<DocumentService> _logger;

        // Các extension đư?c phép upload
        private static readonly HashSet<string> AllowedExtensions = new()
        {
            ".pdf", ".txt", ".docx", ".md", ".doc"
        };

        // Các content type đư?c phép
        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "application/pdf",
            "text/plain",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/markdown",
            "application/msword"
        };

        public DocumentService(
            IGroupAttachmentRepository attachmentRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IFileStorageService fileStorageService,
            IVectorDatabaseService vectorDbService,
            IEmbeddingService embeddingService,
            IUserRepository userRepository,
            IServiceScopeFactory serviceScopeFactory,
            ILogger<DocumentService> logger)
        {
            _attachmentRepository = attachmentRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _fileStorageService = fileStorageService;
            _vectorDbService = vectorDbService;
            _embeddingService = embeddingService;
            _userRepository = userRepository;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        /// <summary>
        /// BƯ?C 1: Request upload URL
        /// Validate permission ? Create metadata ? Generate presigned URL
        /// </summary>
        public async Task<RequestDocumentUploadResponse> RequestUploadAsync(
            Guid userId,
            RequestDocumentUploadRequest request)
        {
            // Ki?m tra user có ph?i member c?a group không
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                request.GroupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Validate file extension
            string extension = Path.GetExtension(request.FileName).ToLower();
            if (!AllowedExtensions.Contains(extension))
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidFileFormat,
                    StatusCodes.Status400BadRequest);
            }

            // Validate content type
            if (!AllowedContentTypes.Contains(request.ContentType))
            {
                throw new AppException(
                    ErrorCodes.ValidationInvalidFileFormat,
                    StatusCodes.Status400BadRequest);
            }

            // Validate file size (max 10MB)
            if (request.FileSize > 10 * 1024 * 1024)
            {
                throw new AppException(
                    ErrorCodes.ValidationFileSizeExceeded,
                    StatusCodes.Status400BadRequest);
            }

            // T?o attachment ID và file key cho B2
            Guid attachmentId = Guid.NewGuid();
            string fileKey = $"group_{request.GroupId}/doc_{attachmentId}{extension}";

            // T?o metadata trong database
            GroupAttachment attachment = new GroupAttachment
            {
                GroupAttachmentId = attachmentId,
                GroupId = request.GroupId,
                UploadedBy = userId,
                FileName = request.FileName,
                FileType = request.ContentType,
                FileSize = request.FileSize,
                FileUrl = fileKey,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = DocumentStatus.Uploading,
                IsDeleted = false
            };

            await _attachmentRepository.CreateAsync(attachment);

            // Generate presigned URL đ? frontend upload tr?c ti?p lên B2
            string presignedUrl = await _fileStorageService.GeneratePresignedUploadUrlAsync(
                fileKey,
                expirationMinutes: 60);

            _logger.LogInformation("Document upload requested. AttachmentId: {AttachmentId}, GroupId: {GroupId}",
                attachmentId, request.GroupId);

            return new RequestDocumentUploadResponse
            {
                AttachmentId = attachmentId,
                UploadUrl = presignedUrl,
                FileKey = fileKey,
                ExpiresIn = 3600
            };
        }

        /// <summary>
        /// BƯ?C 3: Complete upload
        /// Verify file exists ? Update status ? Queue background job
        /// Frontend g?i sau khi upload lên B2 thành công
        /// </summary>
        public async Task CompleteUploadAsync(Guid userId, Guid attachmentId)
        {
            GroupAttachment? attachment = await _attachmentRepository.GetByIdAsync(attachmentId);

            if (attachment == null || attachment.UploadedBy != userId)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status404NotFound);
            }

            // Verify file đ? đư?c upload lên B2 thành công
            bool fileExists = await _fileStorageService.FileExistsAsync(attachment.FileUrl);
            if (!fileExists)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status400BadRequest);
            }

            // Update status sang Processing
            attachment.ProcessingStatus = DocumentStatus.Processing;
            await _attachmentRepository.UpdateAsync(attachment);

            // B?t đ?u background job đ? x? l? document
            // T?o scope mới đ? tr?nh DbContext disposed
            _ = Task.Run(async () =>
            {
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    await ProcessDocumentInScopeAsync(attachmentId, scope.ServiceProvider);
                }
            });

            _logger.LogInformation("Document upload completed. Starting background processing. AttachmentId: {AttachmentId}",
                attachmentId);
        }

        /// <summary>
        /// Wrapper cho background processing với scope riêng
        /// </summary>
        private async Task ProcessDocumentInScopeAsync(Guid attachmentId, IServiceProvider serviceProvider)
        {
            var attachmentRepository = serviceProvider.GetRequiredService<IGroupAttachmentRepository>();
            var fileStorageService = serviceProvider.GetRequiredService<IFileStorageService>();
            var embeddingService = serviceProvider.GetRequiredService<IEmbeddingService>();
            var vectorDbService = serviceProvider.GetRequiredService<IVectorDatabaseService>();
            var logger = serviceProvider.GetRequiredService<ILogger<DocumentService>>();

            await ProcessDocumentAsync(
                attachmentId,
                attachmentRepository,
                fileStorageService,
                embeddingService,
                vectorDbService,
                logger);
        }

        /// <summary>
        /// BƯỚC 4: Background processing
        /// Download → Extract text → Chunk → Generate embeddings → Upsert to Qdrant
        /// Chạy trong background, không block request
        /// </summary>
        private async Task ProcessDocumentAsync(
            Guid attachmentId,
            IGroupAttachmentRepository attachmentRepository,
            IFileStorageService fileStorageService,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDbService,
            ILogger<DocumentService> logger)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                // Lấy thông tin attachment từ database
                GroupAttachment? attachment = await attachmentRepository.GetByIdAsync(attachmentId);
                if (attachment == null)
                {
                    logger.LogWarning("Attachment not found for processing: {AttachmentId}", attachmentId);
                    return;
                }

                logger.LogInformation("Processing document: {AttachmentId}, File: {FileName}",
                    attachmentId, attachment.FileName);

                // Download file từ B2
                Stream fileStream = await fileStorageService.DownloadFileAsync(attachment.FileUrl);

                // Extract text từ file
                string fullText = await ExtractTextAsync(fileStream, attachment.FileName);

                fileStream.Dispose();

                // Chunk text thành các đoạn nh? (max 3200 chars ~ 800 tokens)
                List<string> chunks = ChunkText(fullText, maxChars: 3200);

                logger.LogInformation("Document chunked: {ChunkCount} chunks", chunks.Count);

                // Generate embeddings cho t?t c? chunks (batch)
                List<float[]> embeddings = await embeddingService.GenerateBatchEmbeddingsAsync(
                    chunks,
                    CancellationToken.None);

                // Upsert t?ng chunk vào Qdrant
                for (int i = 0; i < chunks.Count; i++)
                {
                    // Vector ID format: groupId_documentId_chunkIndex
                    string rawId = $"{attachment.GroupAttachmentId}_{i}";
                    string vectorId = GenerateDeterministicUuid(rawId);

                    // Payload ch?a metadata c?a chunk
                    Dictionary<string, object> payload = new Dictionary<string, object>
                    {
                        ["groupId"] = attachment.GroupId.ToString(),
                        ["documentId"] = attachment.GroupAttachmentId.ToString(),
                        ["userId"] = attachment.UploadedBy.ToString(),
                        ["chunkIndex"] = i,
                        ["chunkCount"] = chunks.Count,
                        ["content"] = chunks[i],
                        ["fileName"] = attachment.FileName,
                        ["fileKey"] = attachment.FileUrl,
                        ["embeddingModel"] = _embeddingService.ModelName,
                        ["createdAt"] = DateTime.UtcNow.ToString("o"),
                        ["deleted"] = false
                    };

                    // Upsert vector vào Qdrant
                    await vectorDbService.UpsertVectorAsync(
                        vectorId,
                        embeddings[i],
                        payload);
                }

                // Update status sang Completed
                attachment.ProcessingStatus = DocumentStatus.Completed;
                attachment.ChunkCount = chunks.Count;
                attachment.ProcessedAt = DateTime.UtcNow;
                await attachmentRepository.UpdateAsync(attachment);

                sw.Stop();
                logger.LogInformation(
                    "Document processing completed: {AttachmentId}, Chunks: {ChunkCount}, Time: {Ms}ms",
                    attachmentId, chunks.Count, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process document: {AttachmentId}", attachmentId);

                // Update status sang Failed và lưu error message
                GroupAttachment? attachment = await attachmentRepository.GetByIdAsync(attachmentId);
                if (attachment != null)
                {
                    attachment.ProcessingStatus = DocumentStatus.Failed;
                    attachment.ErrorMessage = ex.Message;
                    await attachmentRepository.UpdateAsync(attachment);
                }
            }
        }

        /// <summary>
        /// Extract text t? file d?a vào extension
        /// H? tr?: .txt, .md, .pdf, .docx
        /// </summary>
        private async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();

            return extension switch
            {
                ".txt" or ".md" => await ExtractFromTextAsync(fileStream),
                ".pdf" => await ExtractFromPdfAsync(fileStream),
                ".docx" => await ExtractFromWordAsync(fileStream),
                _ => throw new NotSupportedException($"File type {extension} not supported for text extraction")
            };
        }

        /// <summary>
        /// Extract text t? file .txt và .md
        /// </summary>
        private async Task<string> ExtractFromTextAsync(Stream fileStream)
        {
            using StreamReader reader = new StreamReader(fileStream);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Extract text t? file PDF
        /// TODO: Implement PDF text extraction
        /// </summary>
        private async Task<string> ExtractFromPdfAsync(Stream fileStream)
        {
            await Task.CompletedTask;
            return "[PDF text extraction not yet implemented]";
        }

        /// <summary>
        /// Extract text t? file Word (.docx)
        /// TODO: Implement Word text extraction
        /// </summary>
        private async Task<string> ExtractFromWordAsync(Stream fileStream)
        {
            await Task.CompletedTask;
            return "[Word text extraction not yet implemented]";
        }

        /// <summary>
        /// Chunk text thành các đo?n nh? đ? t?o embeddings
        /// Strategy: Split by paragraphs, max ~800 tokens per chunk (3200 chars)
        /// </summary>
        private List<string> ChunkText(string text, int maxChars = 3200)
        {
            List<string> chunks = new List<string>();

            // Split text thành paragraphs
            string[] paragraphs = text.Split(
                new[] { "\n\n", "\r\n\r\n" },
                StringSplitOptions.RemoveEmptyEntries);

            System.Text.StringBuilder currentChunk = new System.Text.StringBuilder();

            foreach (string paragraph in paragraphs)
            {
                // N?u thêm paragraph vào chunk hi?n t?i vư?t quá maxChars
                if (currentChunk.Length + paragraph.Length > maxChars)
                {
                    // Lưu chunk hi?n t?i n?u có
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                    }

                    // N?u paragraph quá dài, split thêm
                    if (paragraph.Length > maxChars)
                    {
                        for (int i = 0; i < paragraph.Length; i += maxChars)
                        {
                            int length = Math.Min(maxChars, paragraph.Length - i);
                            chunks.Add(paragraph.Substring(i, length));
                        }
                    }
                    else
                    {
                        currentChunk.Append(paragraph);
                    }
                }
                else
                {
                    currentChunk.Append(paragraph);
                    currentChunk.Append("\n\n");
                }
            }

            // Lưu chunk cu?i cùng
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// L?y tr?ng thái processing c?a document
        /// </summary>
        public async Task<DocumentStatusResponse> GetDocumentStatusAsync(Guid userId, Guid attachmentId)
        {
            GroupAttachment? attachment = await _attachmentRepository.GetByIdAsync(attachmentId);

            if (attachment == null)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status404NotFound);
            }

            // Ki?m tra user có quy?n xem document không
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                attachment.GroupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Tính progress d?a vào status
            int? progress = attachment.ProcessingStatus switch
            {
                DocumentStatus.Uploading => 25,
                DocumentStatus.Processing => 50,
                DocumentStatus.Completed => 100,
                DocumentStatus.Failed => 0,
                _ => 0
            };

            // Message mô t? status
            string message = attachment.ProcessingStatus switch
            {
                DocumentStatus.Uploading => "Waiting for file upload",
                DocumentStatus.Processing => $"Generating embeddings ({attachment.ChunkCount ?? 0} chunks)",
                DocumentStatus.Completed => "Processing completed",
                DocumentStatus.Failed => "Processing failed",
                _ => "Unknown status"
            };

            return new DocumentStatusResponse
            {
                AttachmentId = attachment.GroupAttachmentId,
                FileName = attachment.FileName,
                Status = attachment.ProcessingStatus?.ToString() ?? "Unknown",
                ChunkCount = attachment.ChunkCount,
                Progress = progress,
                Message = message,
                ErrorMessage = attachment.ErrorMessage,
                CreatedAt = attachment.UploadedAt,
                ProcessedAt = attachment.ProcessedAt
            };
        }

        /// <summary>
        /// L?y danh sách t?t c? documents trong group
        /// </summary>
        public async Task<GroupDocumentsResponse> GetGroupDocumentsAsync(Guid userId, Guid groupId)
        {
            // Ki?m tra user có ph?i member c?a group không
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                groupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // L?y t?t c? attachments c?a group
            List<GroupAttachment> attachments = await _attachmentRepository.GetByGroupIdAsync(groupId);

            // L?y thông tin uploaders
            List<Guid> uploaderIds = attachments.Select(a => a.UploadedBy).Distinct().ToList();
            List<User> uploaders = await _userRepository.GetByIdsAsync(uploaderIds);

            // Map sang DTOs
            List<DocumentItem> documentItems = attachments.Select(a =>
            {
                User? uploader = uploaders.FirstOrDefault(u => u.UserId == a.UploadedBy);

                return new DocumentItem
                {
                    AttachmentId = a.GroupAttachmentId,
                    FileName = a.FileName,
                    ContentType = a.FileType,
                    FileSize = a.FileSize,
                    Status = a.ProcessingStatus?.ToString() ?? "Unknown",
                    ChunkCount = a.ChunkCount,
                    UploadedBy = uploader != null ? new UserDto
                    {
                        Id = uploader.UserId,
                        FirstName = uploader.FirstName,
                        LastName = uploader.LastName,
                        AvatarUrl = uploader.AvatarUrl
                    } : new UserDto(),
                    CreatedAt = a.UploadedAt
                };
            }).ToList();

            return new GroupDocumentsResponse
            {
                Documents = documentItems,
                TotalCount = documentItems.Count
            };
        }

        /// <summary>
        /// Xóa document (soft delete)
        /// - Set IsDeleted = true trong database
        /// - Delete vectors t? Qdrant
        /// </summary>
        public async Task DeleteDocumentAsync(Guid userId, Guid attachmentId)
        {
            GroupAttachment? attachment = await _attachmentRepository.GetByIdAsync(attachmentId);

            if (attachment == null)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status404NotFound);
            }

            // Ki?m tra permission: User ph?i là uploader ho?c member c?a group
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                attachment.GroupId,
                userId);

            if (!isMember && attachment.UploadedBy != userId)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Soft delete trong database
            attachment.IsDeleted = true;
            await _attachmentRepository.UpdateAsync(attachment);

            // Delete vectors t? Qdrant
            if (attachment.ChunkCount.HasValue)
            {
                for (int i = 0; i < attachment.ChunkCount.Value; i++)
                {
                    string vectorId = $"{attachment.GroupId}_{attachment.GroupAttachmentId}_{i}";
                    await _vectorDbService.DeleteVectorAsync(vectorId);
                }
            }

            _logger.LogInformation("Document deleted: {AttachmentId}, Chunks deleted: {ChunkCount}",
                attachmentId, attachment.ChunkCount ?? 0);
        }
        private string GenerateDeterministicUuid(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return new Guid(hash).ToString();
        }
    }
}
