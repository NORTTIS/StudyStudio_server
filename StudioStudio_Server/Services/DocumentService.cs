using Microsoft.AspNetCore.Http;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using StudioStudio_Server.Services.EmbeddingQueue;
using StudioStudio_Server.Services.DeleteQueue;
using StudioStudio_Server.Utils;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace StudioStudio_Server.Services
{
    /// <summary>
    /// Service for document upload, processing and embedding
    /// Flow: Request Upload → Upload to B2 → Complete → Queue → Background Processing → Qdrant
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
        private readonly IEmbeddingQueue _embeddingQueue;
        private readonly IDeleteQueue _deleteQueue;
        private readonly ILogger<DocumentService> _logger;
        private readonly IGroupRepository _groupRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICacheService _cacheService;

        // Allowed file extensions for upload
        private static readonly HashSet<string> AllowedExtensions = new()
        {
            ".pdf", ".txt", ".docx", ".md", ".doc"
        };

        // Allowed content types
        private static readonly HashSet<string> AllowedContentTypes = new()
        {
            "application/pdf",
            "text/plain",
            "text/markdown",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
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
            IEmbeddingQueue embeddingQueue,
            IDeleteQueue deleteQueue,
            ILogger<DocumentService> logger,
            IGroupRepository groupRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IHttpContextAccessor httpContextAccessor,
            ICacheService cacheService)
        {
            _attachmentRepository = attachmentRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _fileStorageService = fileStorageService;
            _vectorDbService = vectorDbService;
            _embeddingService = embeddingService;
            _userRepository = userRepository;
            _serviceScopeFactory = serviceScopeFactory;
            _embeddingQueue = embeddingQueue;
            _deleteQueue = deleteQueue;
            _logger = logger;
            _groupRepository = groupRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _httpContextAccessor = httpContextAccessor;
            _cacheService = cacheService;
        }

        /// <summary>
        /// STEP 1: Request upload URL
        /// Validate permission → Check storage quota → Create metadata → Generate presigned URL
        /// </summary>
        public async Task<RequestDocumentUploadResponse> RequestUploadAsync(
            Guid userId,
            RequestDocumentUploadRequest request)
        {
            // Check if user is a member of the group
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                request.GroupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group != null && group.IsArchived)
            {
                var userRole = await _groupParticipantRepository.GetGroupRoleByUserIdAsync(userId, request.GroupId);
                if (userRole != GroupRole.Owner)
                {
                    throw new AppException(ErrorCodes.GroupIsArchived, StatusCodes.Status403Forbidden);
                }
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

            // Validate file size (max 10MB per file)
            if (request.FileSize > 10 * 1024 * 1024)
            {
                throw new AppException(
                    ErrorCodes.ValidationFileSizeExceeded,
                    StatusCodes.Status400BadRequest);
            }

            // Check storage quota for group
            Guid groupOwnerId = await _groupRepository.GetGroupOwnerIdAsync(request.GroupId);

            // Get owner's subscription plan
            SubscriptionPlan? subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(groupOwnerId);
            int storageLimit = subscriptionPlan?.MaxStorageMb ?? 500; // Default 500MB
            long storageLimitBytes = storageLimit * 1024L * 1024L;

            // Get current storage used by group
            long currentStorageUsed = await _attachmentRepository.GetTotalStorageUsedByGroupAsync(request.GroupId);

            // Check if adding this file exceeds storage quota
            if (currentStorageUsed + request.FileSize > storageLimitBytes)
            {
                long availableStorage = storageLimitBytes - currentStorageUsed;

                _logger.LogWarning(
                    "Storage quota exceeded for group {GroupId}. " +
                    "Current: {CurrentMB:F2}MB, Limit: {LimitMB}MB, " +
                    "Requested: {RequestedMB:F2}MB, Available: {AvailableMB:F2}MB",
                    request.GroupId,
                    currentStorageUsed / (1024.0 * 1024.0),
                    storageLimit,
                    request.FileSize / (1024.0 * 1024.0),
                    availableStorage / (1024.0 * 1024.0));

                throw new AppException(
                    ErrorCodes.StorageQuotaExceeded,
                    StatusCodes.Status403Forbidden);
            }

            _logger.LogInformation(
                "Storage check passed for group {GroupId}. " +
                "Current: {CurrentMB:F2}MB/{LimitMB}MB, " +
                "After upload: {AfterMB:F2}MB ({Percent:F1}%)",
                request.GroupId,
                currentStorageUsed / (1024.0 * 1024.0),
                storageLimit,
                (currentStorageUsed + request.FileSize) / (1024.0 * 1024.0),
                (currentStorageUsed + request.FileSize) / (double)storageLimitBytes * 100);

            // Create attachment ID and file key for B2
            Guid attachmentId = Guid.NewGuid();
            string fileKey = $"group_{request.GroupId}/doc_{attachmentId}{extension}";

            // Create metadata in database
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

            // Generate presigned URL for frontend to upload directly to B2
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
        /// STEP 3: Complete upload
        /// Verify file exists → Update status → Queue background job
        /// Called by frontend after successful upload to B2
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

            // Verify file was successfully uploaded to B2
            bool fileExists = await _fileStorageService.FileExistsAsync(attachment.FileUrl);
            if (!fileExists)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status400BadRequest);
            }

            // Update status to Processing
            attachment.ProcessingStatus = DocumentStatus.Processing;
            await _attachmentRepository.UpdateAsync(attachment);

            // Estimate tokens based on file size
            // Rule of thumb: 1MB ≈ 20,000 characters ≈ 5,000 tokens
            int estimatedTokens = EstimateTokensFromFileSize(attachment.FileSize);

            // Enqueue job for background processing
            var job = new EmbeddingJob
            {
                AttachmentId = attachmentId,
                UserId = userId,
                GroupId = attachment.GroupId,
                FileName = attachment.FileName,
                FileSize = attachment.FileSize,
                QueuedAt = DateTime.UtcNow,
                EstimatedTokens = estimatedTokens,
                RetryCount = 0,
                MaxRetries = 3,
                Priority = CalculateJobPriority(attachment.FileSize)
            };

            await _embeddingQueue.EnqueueAsync(job);

            _logger.LogInformation(
                "Document upload completed and queued for processing. " +
                "AttachmentId: {AttachmentId}, File: {FileName}, " +
                "Size: {Size} bytes, Estimated tokens: {Tokens:N0}, " +
                "Queue depth: {Depth}",
                attachmentId, attachment.FileName,
                attachment.FileSize, estimatedTokens,
                _embeddingQueue.GetQueueDepth());
        }

        /// <summary>
        /// Estimate tokens from file size
        /// Rule of thumb: 1MB ≈ 20,000 characters ≈ 5,000 tokens
        /// </summary>
        private int EstimateTokensFromFileSize(long fileSizeBytes)
        {
            // Convert bytes to MB
            double fileSizeMB = fileSizeBytes / (1024.0 * 1024.0);

            // Estimate: 1MB ≈ 5,000 tokens (conservative estimate)
            int estimatedTokens = (int)(fileSizeMB * 5000);

            // Add 20% buffer for safety
            estimatedTokens = (int)(estimatedTokens * 1.2);

            // Minimum 100 tokens, maximum 100K tokens per file
            return Math.Clamp(estimatedTokens, 100, 100_000);
        }

        /// <summary>
        /// Calculate job priority based on file size
        /// Smaller files get higher priority (lower number = higher priority)
        /// </summary>
        private int CalculateJobPriority(long fileSizeBytes)
        {
            // Priority tiers based on file size
            return fileSizeBytes switch
            {
                < 1_000_000 => 1,      // < 1MB: High priority
                < 3_000_000 => 3,      // < 3MB: Medium-high priority
                < 5_000_000 => 5,      // < 5MB: Medium priority
                < 8_000_000 => 7,      // < 8MB: Medium-low priority
                _ => 9                  // >= 8MB: Low priority
            };
        }

        /// <summary>
        /// Wrapper for background processing with separate scope
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
        /// STEP 4: Background processing
        /// Download → Extract text → Chunk → Generate embeddings → Upsert to Qdrant
        /// Runs in background, does not block request
        /// </summary>
        public async Task ProcessDocumentAsync(
            Guid attachmentId,
            IGroupAttachmentRepository attachmentRepository,
            IFileStorageService fileStorageService,
            IEmbeddingService embeddingService,
            IVectorDatabaseService vectorDbService,
            ILogger logger)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                // Get attachment information from database
                GroupAttachment? attachment = await attachmentRepository.GetByIdAsync(attachmentId);
                if (attachment == null)
                {
                    logger.LogWarning("Attachment not found for processing: {AttachmentId}", attachmentId);
                    return;
                }

                logger.LogInformation("Processing document: {AttachmentId}, File: {FileName}, Size: {FileSize} bytes",
                    attachmentId, attachment.FileName, attachment.FileSize);

                // Download file from B2
                logger.LogInformation("Downloading file from B2: {FileUrl}", attachment.FileUrl);
                Stopwatch downloadSw = Stopwatch.StartNew();
                Stream fileStream = await fileStorageService.DownloadFileAsync(attachment.FileUrl);
                downloadSw.Stop();
                logger.LogInformation("File downloaded in {Ms}ms", downloadSw.ElapsedMilliseconds);

                // Extract text from file
                logger.LogInformation("Extracting text from {Extension} file", Path.GetExtension(attachment.FileName));
                Stopwatch extractSw = Stopwatch.StartNew();
                string fullText = await ExtractTextAsync(fileStream, attachment.FileName);
                extractSw.Stop();
                fileStream.Dispose();

                // Validate extracted text
                if (string.IsNullOrWhiteSpace(fullText))
                {
                    logger.LogWarning("Extracted text is empty for document: {AttachmentId}", attachmentId);
                    throw new InvalidOperationException("No text content could be extracted from the document");
                }

                // Calculate text metrics after extraction
                int textLengthChars = fullText.Length;
                int textLengthBytes = Encoding.UTF8.GetByteCount(fullText);
                int estimatedTokens = EstimateTokenCount(fullText);

                logger.LogInformation(
                    "Text extraction completed in {Ms}ms. " +
                    "Text size: {Chars} characters, {Bytes} bytes, " +
                    "Estimated tokens: ~{Tokens} tokens (using 1 token ≈ 4 chars rule)",
                    extractSw.ElapsedMilliseconds,
                    textLengthChars,
                    textLengthBytes,
                    estimatedTokens);

                // Chunk text into smaller pieces (max 3000 chars ~ 750 tokens)
                logger.LogInformation("Starting text chunking with max {MaxChars} chars per chunk", 3000);
                Stopwatch chunkSw = Stopwatch.StartNew();
                List<string> chunks = ChunkText(fullText, maxChars: 3000);
                chunkSw.Stop();

                // Update queue with total chunks (for progress tracking)
                _embeddingQueue.UpdateJobStatus(attachmentId, EmbeddingJobStatus.Processing, null, 0, chunks.Count);

                // Calculate chunk statistics
                int minChunkSize = chunks.Count > 0 ? chunks.Min(c => c.Length) : 0;
                int maxChunkSize = chunks.Count > 0 ? chunks.Max(c => c.Length) : 0;
                int avgChunkSize = chunks.Count > 0 ? (int)chunks.Average(c => c.Length) : 0;
                int totalChunkChars = chunks.Sum(c => c.Length);
                int totalChunkBytes = chunks.Sum(c => Encoding.UTF8.GetByteCount(c));
                int estimatedTokensPerChunk = avgChunkSize / 4;
                int actualTotalTokens = chunks.Sum(c => c.Length / 4);

                logger.LogInformation(
                    "Text chunking completed in {Ms}ms. " +
                    "Total chunks: {ChunkCount}, " +
                    "Chunk size range: {Min}-{Max} chars, " +
                    "Average chunk size: {Avg} chars (~{Tokens} tokens/chunk)",
                    chunkSw.ElapsedMilliseconds,
                    chunks.Count,
                    minChunkSize,
                    maxChunkSize,
                    avgChunkSize,
                    estimatedTokensPerChunk);

                // Validate chunks
                if (chunks.Count == 0)
                {
                    logger.LogWarning("No chunks created for document: {AttachmentId}", attachmentId);
                    throw new InvalidOperationException("Failed to create chunks from document text");
                }

                // Log detailed processing summary
                logger.LogInformation(
                    "Document processing summary:\n" +
                    "  Original file size: {FileSize} bytes\n" +
                    "  Extracted text: {TextChars} characters, {TextBytes} bytes ({TextTokens} estimated tokens)\n" +
                    "  Total chunks: {ChunkCount}\n" +
                    "  Total chunk size: {ChunkChars} characters, {ChunkBytes} bytes\n" +
                    "  Actual tokens (sum of chunks): {ActualTokens:N0}\n" +
                    "  Chunking efficiency: {Efficiency:F1}% (text preserved after chunking)\n" +
                    "  Estimated API calls needed: {ApiCalls}",
                    attachment.FileSize,
                    textLengthChars,
                    textLengthBytes,
                    estimatedTokens,
                    chunks.Count,
                    totalChunkChars,
                    totalChunkBytes,
                    actualTotalTokens,
                    (double)totalChunkChars / textLengthChars * 100,
                    chunks.Count);

                // Generate embeddings for all chunks (sequential with progress updates)
                logger.LogInformation("Starting embedding generation for {Count} chunks", chunks.Count);
                Stopwatch embeddingSw = Stopwatch.StartNew();
                List<float[]> embeddings = new List<float[]>(chunks.Count);

                for (int i = 0; i < chunks.Count; i++)
                {
                    // Generate embedding for this chunk
                    float[] embedding = await embeddingService.GenerateEmbeddingAsync(chunks[i], CancellationToken.None);
                    embeddings.Add(embedding);

                    // Update progress in queue (every 5 chunks or last chunk)
                    if ((i + 1) % 5 == 0 || i == chunks.Count - 1)
                    {
                        _embeddingQueue.UpdateJobStatus(
                            attachmentId,
                            EmbeddingJobStatus.Processing,
                            null,
                            i + 1,
                            chunks.Count);

                        logger.LogInformation(
                            "Embedding progress: {Current}/{Total} ({Percent:F0}%)",
                            i + 1, chunks.Count, (i + 1) * 100.0 / chunks.Count);
                    }
                }

                embeddingSw.Stop();

                // Validate embeddings
                if (embeddings.Count != chunks.Count)
                {
                    logger.LogError("Embeddings count mismatch. Chunks: {ChunkCount}, Embeddings: {EmbeddingCount}",
                        chunks.Count, embeddings.Count);
                    throw new InvalidOperationException(
                        $"Embeddings count ({embeddings.Count}) does not match chunks count ({chunks.Count})");
                }

                logger.LogInformation(
                    "Embedding generation completed in {Ms}ms. Average: {AvgMs}ms per chunk",
                    embeddingSw.ElapsedMilliseconds,
                    embeddingSw.ElapsedMilliseconds / chunks.Count);

                // Upsert each chunk to Qdrant
                logger.LogInformation("Starting vector upsert to Qdrant for {Count} chunks", chunks.Count);
                Stopwatch upsertSw = Stopwatch.StartNew();

                for (int i = 0; i < chunks.Count; i++)
                {
                    // Vector ID format: documentId_chunkIndex
                    string rawId = $"{attachment.GroupAttachmentId}_{i}";
                    string vectorId = GenerateDeterministicUuid(rawId);

                    // Payload contains chunk metadata
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
                        ["embeddingModel"] = embeddingService.ModelName,
                        ["createdAt"] = DateTime.UtcNow.ToString("o"),
                        ["deleted"] = false
                    };

                    // Upsert vector to Qdrant
                    await vectorDbService.UpsertVectorAsync(
                        vectorId,
                        embeddings[i],
                        payload);
                }

                upsertSw.Stop();
                logger.LogInformation(
                    "Vector upsert completed in {Ms}ms. Average: {AvgMs}ms per vector",
                    upsertSw.ElapsedMilliseconds,
                    upsertSw.ElapsedMilliseconds / chunks.Count);

                // Update status to Completed
                attachment.ProcessingStatus = DocumentStatus.Completed;
                attachment.ChunkCount = chunks.Count;
                attachment.ProcessedAt = DateTime.UtcNow;
                await attachmentRepository.UpdateAsync(attachment);

                // Update actual token usage in queue
                _embeddingQueue.UpdateActualTokens(attachmentId, actualTotalTokens);

                sw.Stop();
                logger.LogInformation(
                    "✅ Document processing completed successfully:\n" +
                    "  AttachmentId: {AttachmentId}\n" +
                    "  Total time: {TotalMs}ms ({TotalSeconds:F1}s)\n" +
                    "  Breakdown: Download={DownloadMs}ms, Extract={ExtractMs}ms, Chunk={ChunkMs}ms, Embedding={EmbeddingMs}ms, Upsert={UpsertMs}ms\n" +
                    "  Chunks created: {ChunkCount}\n" +
                    "  Total tokens processed: {TotalTokens:N0}",
                    attachmentId,
                    sw.ElapsedMilliseconds,
                    sw.ElapsedMilliseconds / 1000.0,
                    downloadSw.ElapsedMilliseconds,
                    extractSw.ElapsedMilliseconds,
                    chunkSw.ElapsedMilliseconds,
                    embeddingSw.ElapsedMilliseconds,
                    upsertSw.ElapsedMilliseconds,
                    chunks.Count,
                    actualTotalTokens);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process document: {AttachmentId}", attachmentId);

                // Update status to Failed
                GroupAttachment? attachment = await attachmentRepository.GetByIdAsync(attachmentId);
                if (attachment != null)
                {
                    attachment.ProcessingStatus = DocumentStatus.Failed;
                    attachment.ErrorMessage = ex.Message;
                    await attachmentRepository.UpdateAsync(attachment);
                }

                throw;
            }
        }

        /// <summary>
        /// Estimates the number of tokens in a text string.
        /// Uses a simple heuristic: 1 token ≈ 4 characters for English text.
        /// This is an approximation - actual tokenization depends on the model.
        /// </summary>
        /// <param name="text">Text to estimate token count for</param>
        /// <returns>Estimated number of tokens</returns>
        /// <remarks>
        /// Token estimation rules:
        /// - English: ~1 token per 4 characters (e.g., "hello" = 1 token, 5 chars)
        /// - Vietnamese/Mixed: May vary, but 4 chars/token is a reasonable estimate
        /// - Actual token count depends on tokenizer (GPT uses BPE, Gemini uses SentencePiece)
        /// 
        /// For more accurate estimation, consider using a proper tokenizer library.
        /// </remarks>
        private int EstimateTokenCount(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            // Simple heuristic: 1 token ≈ 4 characters
            // This is based on OpenAI's rule of thumb for English text
            return text.Length / 4;
        }

        /// <summary>
        /// Extract text from file based on extension
        /// Supports: .txt, .md, .pdf, .docx
        /// </summary>
        private async Task<string> ExtractTextAsync(Stream fileStream, string fileName)
        {
            // FIX A: Reset stream position to beginning (critical bug fix)
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            string extension = Path.GetExtension(fileName).ToLower();

            string rawText = extension switch
            {
                ".txt" or ".md" => await ExtractFromTextAsync(fileStream),
                ".pdf" => await ExtractFromPdfAsync(fileStream),
                ".docx" => await ExtractFromWordAsync(fileStream),
                _ => throw new NotSupportedException($"File type {extension} not supported for text extraction")
            };

            // FIX 9: Add content length guard
            if (rawText.Length > 1_000_000)
            {
                _logger.LogWarning("Extracted text too large: {Length} characters, truncating", rawText.Length);
                rawText = rawText.Substring(0, 1_000_000);
            }

            return NormalizeWhitespace(rawText);
        }

        /// <summary>
        /// Extract text from .txt and .md files
        /// </summary>
        private async Task<string> ExtractFromTextAsync(Stream fileStream)
        {
            using StreamReader reader = new StreamReader(fileStream);
            return await reader.ReadToEndAsync();
        }

        /// <summary>
        /// Extract text from PDF file
        /// TODO: Implement PDF text extraction
        /// </summary>
        private async Task<string> ExtractFromPdfAsync(Stream fileStream)
        {
            try
            {
                StringBuilder text = new StringBuilder();
                const int MAX_PAGES = 500; // Prevent memory issues with huge PDFs

                using (PdfReader pdfReader = new PdfReader(fileStream))
                using (PdfDocument pdfDoc = new PdfDocument(pdfReader))
                {
                    int pageCount = pdfDoc.GetNumberOfPages();

                    // FIX 3: Early detection for empty/scan PDFs
                    if (pageCount == 0)
                    {
                        _logger.LogWarning("PDF has no pages");
                        return "[PDF contains no pages]";
                    }

                    // FIX 3: Limit page processing to prevent memory issues
                    int pagesToProcess = Math.Min(pageCount, MAX_PAGES);
                    if (pageCount > MAX_PAGES)
                    {
                        _logger.LogWarning("PDF has {PageCount} pages, processing only first {MaxPages}",
                            pageCount, MAX_PAGES);
                    }

                    for (int page = 1; page <= pagesToProcess; page++)
                    {
                        // FIX C: Use LocationTextExtractionStrategy for better layout preservation
                        ITextExtractionStrategy strategy = new LocationTextExtractionStrategy();
                        string pageText = PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(page), strategy);

                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            // FIX 2: Pre-clean before append to reduce memory
                            pageText = Regex.Replace(pageText, @"\s+", " ");
                            text.AppendLine(pageText.Trim());
                            text.AppendLine();
                        }

                        // FIX 3: Early stop if content is getting too large
                        if (text.Length > 900_000)
                        {
                            _logger.LogWarning("PDF content exceeds 900k chars at page {Page}, stopping extraction", page);
                            break;
                        }
                    }
                }

                string result = text.ToString().Trim();

                // FIX 6: Better scan detection message
                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogWarning("PDF text extraction resulted in empty content - may be scanned/image-based");
                    return "[PDF contains no extractable text content - may require OCR processing]";
                }

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF");
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError,
                    ex);
            }
        }

        /// <summary>
        /// Extract text from Word (.docx) file using DocumentFormat.OpenXml
        /// Extracts text from body, headers, footers, footnotes, and endnotes
        /// </summary>
        private async Task<string> ExtractFromWordAsync(Stream fileStream)
        {
            try
            {
                StringBuilder text = new StringBuilder();

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(fileStream, false))
                {
                    if (wordDoc.MainDocumentPart?.Document?.Body == null)
                    {
                        _logger.LogWarning("DOCX has no body content");
                        return "[DOCX contains no body content]";
                    }

                    // FIX D: Extract headers (all sections)
                    if (wordDoc.MainDocumentPart.HeaderParts != null)
                    {
                        foreach (var headerPart in wordDoc.MainDocumentPart.HeaderParts)
                        {
                            if (headerPart.Header != null)
                            {
                                string headerText = ExtractFromDocumentPartText(headerPart.Header);
                                if (!string.IsNullOrWhiteSpace(headerText))
                                {
                                    text.AppendLine(headerText);
                                    text.AppendLine();
                                }
                            }
                        }
                    }

                    // Extract main body
                    Body body = wordDoc.MainDocumentPart.Document.Body;

                    foreach (var element in body.ChildElements)
                    {
                        if (element is Paragraph paragraph)
                        {
                            string paragraphText = ExtractParagraphText(paragraph);
                            if (!string.IsNullOrWhiteSpace(paragraphText))
                            {
                                text.AppendLine(paragraphText);
                                text.AppendLine();
                            }
                        }
                        else if (element is Table table)
                        {
                            string tableText = ExtractTableText(table);
                            if (!string.IsNullOrWhiteSpace(tableText))
                            {
                                text.AppendLine(tableText);
                                text.AppendLine();
                            }
                        }

                        // FIX 3: Early stop if content is getting too large
                        if (text.Length > 900_000)
                        {
                            _logger.LogWarning("DOCX content exceeds 900k chars, stopping extraction");
                            break;
                        }
                    }

                    // FIX D: Extract footers (all sections)
                    if (wordDoc.MainDocumentPart.FooterParts != null)
                    {
                        foreach (var footerPart in wordDoc.MainDocumentPart.FooterParts)
                        {
                            if (footerPart.Footer != null)
                            {
                                string footerText = ExtractFromDocumentPartText(footerPart.Footer);
                                if (!string.IsNullOrWhiteSpace(footerText))
                                {
                                    text.AppendLine(footerText);
                                    text.AppendLine();
                                }
                            }
                        }
                    }

                    // FIX D: Extract footnotes
                    if (wordDoc.MainDocumentPart.FootnotesPart?.Footnotes != null)
                    {
                        string footnotesText = ExtractFromDocumentPartText(wordDoc.MainDocumentPart.FootnotesPart.Footnotes);
                        if (!string.IsNullOrWhiteSpace(footnotesText))
                        {
                            text.AppendLine("--- Footnotes ---");
                            text.AppendLine(footnotesText);
                            text.AppendLine();
                        }
                    }

                    // FIX D: Extract endnotes
                    if (wordDoc.MainDocumentPart.EndnotesPart?.Endnotes != null)
                    {
                        string endnotesText = ExtractFromDocumentPartText(wordDoc.MainDocumentPart.EndnotesPart.Endnotes);
                        if (!string.IsNullOrWhiteSpace(endnotesText))
                        {
                            text.AppendLine("--- Endnotes ---");
                            text.AppendLine(endnotesText);
                            text.AppendLine();
                        }
                    }
                }

                string result = text.ToString().Trim();

                if (string.IsNullOrWhiteSpace(result))
                {
                    _logger.LogWarning("DOCX text extraction resulted in empty content");
                    return "[DOCX contains no extractable text content]";
                }

                // FIX 5: Remove duplicate lines (common in headers/footers)
                result = RemoveDuplicateLines(result);

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from DOCX");
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError,
                    ex);
            }
        }

        /// <summary>
        /// Extract text from any OpenXML element (headers, footers, footnotes, etc.)
        /// </summary>
        private string ExtractFromDocumentPartText(OpenXmlElement element)
        {
            StringBuilder text = new StringBuilder();

            foreach (var paragraph in element.Descendants<Paragraph>())
            {
                string paragraphText = ExtractParagraphText(paragraph);
                if (!string.IsNullOrWhiteSpace(paragraphText))
                {
                    text.AppendLine(paragraphText);
                }
            }

            return text.ToString();
        }

        /// <summary>
        /// Remove duplicate lines that appear more than a threshold (common in headers/footers)
        /// </summary>
        private string RemoveDuplicateLines(string text, int threshold = 3)
        {
            var lines = text.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);
            var lineCount = new Dictionary<string, int>();

            // Count occurrences of each non-empty line
            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedLine))
                {
                    if (lineCount.ContainsKey(trimmedLine))
                    {
                        lineCount[trimmedLine]++;
                    }
                    else
                    {
                        lineCount[trimmedLine] = 1;
                    }
                }
            }

            // Filter out lines that repeat too many times (likely headers/footers)
            var result = new StringBuilder();
            var addedLines = new HashSet<string>();

            foreach (var line in lines)
            {
                string trimmedLine = line.Trim();

                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    result.AppendLine(line);
                    continue;
                }

                // If line repeats more than threshold times, only add once
                if (lineCount[trimmedLine] > threshold)
                {
                    if (!addedLines.Contains(trimmedLine))
                    {
                        result.AppendLine(line);
                        addedLines.Add(trimmedLine);
                    }
                }
                else
                {
                    result.AppendLine(line);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Chunk text into smaller pieces for creating embeddings
        /// Strategy: Split by paragraphs, max ~800 tokens per chunk (3200 chars)
        /// </summary>
        private List<string> ChunkText(string text, int maxChars = 3000)
        {
            List<string> chunks = new List<string>();

            // FIX E: Reduced from 3200 to 3000 for better safety margin
            // Reasoning: 3000 chars ≈ 600-900 tokens depending on language
            // This ensures we stay well below typical 1024 token limits

            // Split text into paragraphs
            string[] paragraphs = text.Split(
                new[] { "\n\n", "\r\n\r\n" },
                StringSplitOptions.RemoveEmptyEntries);

            StringBuilder currentChunk = new StringBuilder();
            int estimatedTokens = 0;
            const int MAX_ESTIMATED_TOKENS = 750; // Conservative limit

            foreach (string paragraph in paragraphs)
            {
                string trimmedParagraph = paragraph.Trim();
                if (string.IsNullOrEmpty(trimmedParagraph))
                {
                    continue;
                }

                // FIX E: Rough token estimation (1 token ≈ 4 chars)
                int paragraphTokens = trimmedParagraph.Length / 4;

                // If adding paragraph to current chunk exceeds limits
                if (currentChunk.Length + trimmedParagraph.Length > maxChars ||
                    estimatedTokens + paragraphTokens > MAX_ESTIMATED_TOKENS)
                {
                    // Save current chunk if not empty
                    if (currentChunk.Length > 0)
                    {
                        chunks.Add(currentChunk.ToString().Trim());
                        currentChunk.Clear();
                        estimatedTokens = 0;
                    }

                    // If paragraph itself is too long, split it further
                    if (trimmedParagraph.Length > maxChars)
                    {
                        // Split by sentences first (better semantic boundaries)
                        var sentences = Regex.Split(trimmedParagraph, @"(?<=[.!?])\s+");

                        foreach (var sentence in sentences)
                        {
                            if (currentChunk.Length + sentence.Length > maxChars)
                            {
                                if (currentChunk.Length > 0)
                                {
                                    chunks.Add(currentChunk.ToString().Trim());
                                    currentChunk.Clear();
                                    estimatedTokens = 0;
                                }

                                // If even a single sentence is too long, hard split
                                if (sentence.Length > maxChars)
                                {
                                    for (int i = 0; i < sentence.Length; i += maxChars)
                                    {
                                        int length = Math.Min(maxChars, sentence.Length - i);
                                        chunks.Add(sentence.Substring(i, length).Trim());
                                    }
                                }
                                else
                                {
                                    currentChunk.Append(sentence);
                                    currentChunk.Append(" ");
                                    estimatedTokens += sentence.Length / 4;
                                }
                            }
                            else
                            {
                                currentChunk.Append(sentence);
                                currentChunk.Append(" ");
                                estimatedTokens += sentence.Length / 4;
                            }
                        }
                    }
                    else
                    {
                        currentChunk.Append(trimmedParagraph);
                        currentChunk.Append("\n\n");
                        estimatedTokens += paragraphTokens;
                    }
                }
                else
                {
                    currentChunk.Append(trimmedParagraph);
                    currentChunk.Append("\n\n");
                    estimatedTokens += paragraphTokens;
                }
            }

            // Save last chunk
            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }

            return chunks;
        }

        /// <summary>
        /// Get document processing status
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

            // Check if user has permission to view document
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                attachment.GroupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Check actual status from embedding queue
            var queueStatus = _embeddingQueue.GetJobStatus(attachmentId);

            // Calculate progress and status message
            int? progress = null;
            string message = string.Empty;

            if (queueStatus != null)
            {
                // Job is in queue or being processed
                progress = queueStatus.Status switch
                {
                    EmbeddingJobStatus.Queued => 10,
                    EmbeddingJobStatus.Processing when queueStatus.TotalChunks > 0 =>
                        10 + (int)((queueStatus.ProcessedChunks / (double)queueStatus.TotalChunks) * 90),
                    EmbeddingJobStatus.Completed => 100,
                    EmbeddingJobStatus.Failed => 0,
                    _ => 0
                };

                message = queueStatus.Status switch
                {
                    EmbeddingJobStatus.Queued => $"Queued for indexing (position: {_embeddingQueue.GetQueueDepth()})",
                    EmbeddingJobStatus.Processing => $"Indexing document ({queueStatus.ProcessedChunks}/{queueStatus.TotalChunks} chunks)",
                    EmbeddingJobStatus.Completed => "Indexing completed",
                    EmbeddingJobStatus.Failed => $"Indexing failed: {queueStatus.ErrorMessage}",
                    _ => "Unknown status"
                };
            }
            else
            {
                // If not found, use status from database
                progress = attachment.ProcessingStatus switch
                {
                    DocumentStatus.Uploading => 5,
                    DocumentStatus.Processing => 50,
                    DocumentStatus.Completed => 100,
                    DocumentStatus.Failed => 0,
                    _ => 0
                };

                message = attachment.ProcessingStatus switch
                {
                    DocumentStatus.Uploading => "Waiting for file upload",
                    DocumentStatus.Processing => $"Processing ({attachment.ChunkCount ?? 0} chunks)",
                    DocumentStatus.Completed => "Processing completed",
                    DocumentStatus.Failed => "Processing failed",
                    _ => "Unknown status"
                };
            }

            return new DocumentStatusResponse
            {
                AttachmentId = attachment.GroupAttachmentId,
                FileName = attachment.FileName,
                Status = attachment.ProcessingStatus?.ToString() ?? "Unknown",
                ChunkCount = attachment.ChunkCount ?? queueStatus?.TotalChunks,
                Progress = progress,
                Message = message,
                ErrorMessage = attachment.ErrorMessage ?? queueStatus?.ErrorMessage,
                CreatedAt = attachment.UploadedAt,
                ProcessedAt = attachment.ProcessedAt
            };
        }

        /// <summary>
        /// Get all documents in a group
        /// </summary>
        public async Task<GroupDocumentsResponse> GetGroupDocumentsAsync(Guid userId, Guid groupId)
        {
            // Check if user is a member of the group
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                groupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Get all attachments for the group
            List<GroupAttachment> attachments = await _attachmentRepository.GetByGroupIdAsync(groupId);

            // Get uploader information
            List<Guid> uploaderIds = attachments.Select(a => a.UploadedBy).Distinct().ToList();
            List<User> uploaders = await _userRepository.GetByIdsAsync(uploaderIds);

            // Map to DTOs
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
                        AvatarUrl = AvatarUrlHelper.BuildAbsoluteAvatarUrl(uploader.AvatarUrl, _httpContextAccessor.HttpContext)
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
        /// Delete document permanently
        /// - Delete B2 blob file
        /// - Enqueue background job to delete vectors from Qdrant
        /// - Hard-delete DB record
        /// - Decrement group storage used
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

            // Check permission: User must be uploader or member of group
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                attachment.GroupId,
                userId);

            if (!isMember && attachment.UploadedBy != userId)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Delete B2 blob file
            try
            {
                await _fileStorageService.DeleteFileAsync(attachment.FileUrl);
                _logger.LogInformation(
                    "B2 blob deleted: AttachmentId={AttachmentId}, FileKey={FileKey}",
                    attachmentId, attachment.FileUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete B2 blob for AttachmentId={AttachmentId}. Continuing with deletion.",
                    attachmentId);
            }

            // Enqueue delete job for background processing (if document was processed)
            if (attachment.ChunkCount.HasValue && attachment.ChunkCount.Value > 0)
            {
                var deleteJob = new DeleteJob
                {
                    AttachmentId = attachmentId,
                    GroupId = attachment.GroupId,
                    UserId = userId,
                    FileName = attachment.FileName,
                    ChunkCount = attachment.ChunkCount.Value,
                    QueuedAt = DateTime.UtcNow,
                    RetryCount = 0,
                    MaxRetries = 3
                };

                await _deleteQueue.EnqueueAsync(deleteJob);

                _logger.LogInformation(
                    "Document deletion queued: AttachmentId={AttachmentId}, " +
                    "ChunkCount={ChunkCount}, Queue depth={Depth}",
                    attachmentId, attachment.ChunkCount, _deleteQueue.GetQueueDepth());
            }

            // Hard-delete DB record
            await _attachmentRepository.HardDeleteAsync(attachmentId);

            // Invalidate AI document cache so AI sees fresh document data immediately
            try
            {
                await _cacheService.InvalidateAIDocumentCacheAsync(userId, attachment.GroupId, null);
                await _cacheService.InvalidateAIDocumentCacheForGroupAsync(attachment.GroupId, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to invalidate AI document cache after hard delete: AttachmentId={AttachmentId}, GroupId={GroupId}",
                    attachmentId, attachment.GroupId);
            }

            _logger.LogInformation(
                "Document hard-deleted: AttachmentId={AttachmentId}, FileSize={FileSize}",
                attachmentId, attachment.FileSize);
        }

        /// <summary>
        /// Generate presigned download URL for document
        /// Validates user permission before generating URL
        /// </summary>
        public async Task<string> GetDocumentDownloadUrlAsync(Guid userId, Guid attachmentId, int expirationMinutes = 60)
        {
            GroupAttachment? attachment = await _attachmentRepository.GetByIdAsync(attachmentId);

            if (attachment == null || attachment.IsDeleted)
            {
                throw new AppException(
                    ErrorCodes.ValidationRequiredField,
                    StatusCodes.Status404NotFound);
            }

            // Check if user has permission to download document
            bool isMember = await _groupParticipantRepository.IsUserInGroupAsync(
                attachment.GroupId,
                userId);

            if (!isMember)
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }

            // Generate presigned download URL
            string downloadUrl = await _fileStorageService.GeneratePresignedDownloadUrlAsync(
                attachment.FileUrl,
                expirationMinutes);

            _logger.LogInformation(
                "Download URL generated: AttachmentId={AttachmentId}, File={FileName}, ExpiresIn={Minutes}min",
                attachmentId, attachment.FileName, expirationMinutes);

            return downloadUrl;
        }

        private string GenerateDeterministicUuid(string input)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            return new Guid(hash).ToString();
        }

        /// <summary>
        /// Extract text from a paragraph, handling runs and text elements properly
        /// </summary>
        private string ExtractParagraphText(Paragraph paragraph)
        {
            StringBuilder paragraphText = new StringBuilder();

            foreach (var run in paragraph.Descendants<Run>())
            {
                foreach (var textElement in run.Descendants<Text>())
                {
                    string textContent = textElement.Text;
                    if (!string.IsNullOrEmpty(textContent))
                    {
                        paragraphText.Append(textContent);
                    }
                }

                // Handle tabs
                if (run.Descendants<TabChar>().Any())
                {
                    paragraphText.Append("\t");
                }

                // Handle line breaks
                if (run.Descendants<Break>().Any())
                {
                    paragraphText.Append(" ");
                }
            }

            return paragraphText.ToString();
        }

        /// <summary>
        /// Extract text from a table with proper formatting
        /// FIX 8: Use pipe separator for better RAG understanding
        /// </summary>
        private string ExtractTableText(Table table)
        {
            StringBuilder tableText = new StringBuilder();

            foreach (var row in table.Descendants<TableRow>())
            {
                List<string> cellTexts = new List<string>();

                foreach (var cell in row.Descendants<TableCell>())
                {
                    StringBuilder cellText = new StringBuilder();

                    foreach (var paragraph in cell.Descendants<Paragraph>())
                    {
                        string paragraphText = ExtractParagraphText(paragraph);
                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            if (cellText.Length > 0)
                            {
                                cellText.Append(" ");
                            }
                            cellText.Append(paragraphText.Trim());
                        }
                    }

                    if (cellText.Length > 0)
                    {
                        cellTexts.Add(cellText.ToString());
                    }
                }

                if (cellTexts.Count > 0)
                {
                    // FIX 8: Use pipe separator instead of tab for better semantic understanding
                    tableText.AppendLine(string.Join(" | ", cellTexts));
                }
            }

            return tableText.ToString();
        }

        /// <summary>
        /// Normalize whitespace in extracted text
        /// FIX 7: Optimized to use fewer regex passes
        /// - Remove excess spaces between words (multiple spaces → single space)
        /// - Remove excess blank lines (multiple newlines → double newline)
        /// - Trim leading/trailing whitespace
        /// </summary>
        private string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            // FIX 7: Combined regex operations for better performance
            // Replace multiple spaces/tabs with single space (but preserve newlines)
            text = Regex.Replace(text, @"[ \t]+", " ");

            // Replace multiple newlines with double newline (paragraph separator)
            text = Regex.Replace(text, @"(\r?\n){3,}", "\n\n");

            // Remove spaces at the beginning and end of lines
            text = Regex.Replace(text, @"^[ \t]+|[ \t]+$", "", RegexOptions.Multiline);

            // Remove trailing/leading whitespace
            return text.Trim();
        }
    }
}
