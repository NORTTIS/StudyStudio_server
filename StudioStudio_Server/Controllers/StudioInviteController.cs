using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Caches;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller for managing Studio Invitations
    /// Route: /api/studio-invite
    /// Uses Redis for token storage (15 min expiry)
    /// </summary>
    [Route("api/studio-invite")]
    [ApiController]
    [Authorize]
    public class StudioInviteController : ControllerBase
    {
        private readonly IStudioInviteService _studioInviteService;
        private readonly IStudioRepository _studioRepository;
        private readonly IStudioParticipantRepository _studioParticipantRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudioInviteController> _logger;

        public StudioInviteController(
            IStudioInviteService studioInviteService,
            IStudioRepository studioRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            IMessageService messageService,
            IConfiguration configuration,
            ILogger<StudioInviteController> logger)
        {
            _studioInviteService = studioInviteService;
            _studioRepository = studioRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _emailService = emailService;
            _userRepository = userRepository;
            _messageService = messageService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Authenticate and get userId from JWT token
        /// Validate: User must not be admin
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
        /// Validate and parse role string to StudioRole enum
        /// Validate: Role must be Member (Owner cannot be invited)
        /// </summary>
        private StudioRole ValidateAndParseRole(string roleString)
        {
            if (!Enum.TryParse<StudioRole>(roleString, true, out StudioRole role))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            if (role == StudioRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            return role;
        }

        /// <summary>
        /// Validate user has permission to create invite (only Owner)
        /// </summary>
        private async Task ValidateInvitePermissionAsync(Guid studioId, Guid userId)
        {
            bool isOwner = await _studioRepository.IsUserStudioOwnerAsync(studioId, userId);

            if (!isOwner)
            {
                throw new AppException(
                    ErrorCodes.StudioPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio-invite/create
        /// Create invite link for studio
        /// Validate:
        /// - User must be Owner
        /// - Role must be valid (cannot be Owner, only Member)
        /// - Must not exceed rate limit (5 links/15 minutes)
        /// Expiry: 15 minutes
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<ApiResponse<CreateStudioInviteResponse>>> CreateInviteLink(
            [FromBody] CreateStudioInviteRequest request)
        {
            var userId = ValidateAndGetUserId();
            var role = ValidateAndParseRole(request.Role);

            var studio = await _studioRepository.GetByIdAsync(request.StudioId);
            if (studio == null)
            {
                throw new AppException(
                    ErrorCodes.StudioNotFound,
                    StatusCodes.Status404NotFound);
            }

            await ValidateInvitePermissionAsync(request.StudioId, userId);

            bool canCreate = await _studioInviteService
                .CheckInviteCreationRateLimitAsync(request.StudioId, userId);

            if (!canCreate)
            {
                throw new AppException(
                    ErrorCodes.InviteRateLimitExceeded,
                    StatusCodes.Status429TooManyRequests);
            }

            string token = await _studioInviteService.GenerateInviteTokenAsync();

            var inviteData = new StudioInviteToken
            {
                StudioId = request.StudioId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            bool stored = await _studioInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }

            string frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            string inviteUrl = $"{frontendUrl}/studio-invite/{token}";

            var response = new CreateStudioInviteResponse
            {
                InviteUrl = inviteUrl,
                Token = token,
                Role = role.ToString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = inviteData.CreatedAt
            };

            _logger.LogInformation(
                "Invite link created for studio {StudioId} with role {Role} by user {UserId}",
                request.StudioId, role, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateInvite);
            return Ok(ApiResponse<CreateStudioInviteResponse>.Success(
                ErrorCodes.SuccessCreateInvite,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio-invite/email
        /// Send invite link via email
        /// Validate:
        /// - User must be Owner
        /// - Role must be valid (cannot be Owner, only Member)
        /// - Must not exceed rate limit (5 links/15 minutes)
        /// Action: Send email with invite link
        /// </summary>
        [HttpPost("email")]
        public async Task<ActionResult<ApiResponse<object>>> SendInviteEmail(
            [FromBody] SendStudioInviteEmailRequest request)
        {
            var userId = ValidateAndGetUserId();
            var role = ValidateAndParseRole(request.Role);

            var studio = await _studioRepository.GetByIdAsync(request.StudioId);
            if (studio == null)
            {
                throw new AppException(
                    ErrorCodes.StudioNotFound,
                    StatusCodes.Status404NotFound);
            }

            await ValidateInvitePermissionAsync(request.StudioId, userId);

            bool canCreate = await _studioInviteService
                .CheckInviteCreationRateLimitAsync(request.StudioId, userId);

            if (!canCreate)
            {
                throw new AppException(
                    ErrorCodes.InviteRateLimitExceeded,
                    StatusCodes.Status429TooManyRequests);
            }

            var inviter = await _userRepository.GetByIdAsync(userId);
            if (inviter == null)
            {
                throw new AppException(
                    ErrorCodes.UserNotFound,
                    StatusCodes.Status404NotFound);
            }

            string token = await _studioInviteService.GenerateInviteTokenAsync();

            var inviteData = new StudioInviteToken
            {
                StudioId = request.StudioId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            bool stored = await _studioInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }

            string frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            string inviteUrl = $"{frontendUrl}/studio-invite/{token}";

            string inviterName = $"{inviter.FirstName} {inviter.LastName}";
            string subject = $"Invitation to join {studio.StudioName} on Study Studio";
            string body = EmailTemplate.StudioInviteEmail(
                inviteUrl,
                inviterName,
                studio.StudioName,
                role.ToString(),
                studio.Description);

            // Check email notification preference if invitee is an existing user
            var invitee = await _userRepository.GetByEmailAsync(request.Email);
            if (invitee != null)
            {
                await _emailService.SendEmailWithPreferenceCheckAsync(request.Email, subject, body, invitee.UserId);
            }
            else
            {
                // Invite to non-existing user - send email directly
                await _emailService.SendLinkAsync(request.Email, subject, body);
            }

            _logger.LogInformation(
                "Invite email sent to {Email} for studio {StudioId} with role {Role} by user {UserId}",
                request.Email, request.StudioId, role, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessSendInviteEmail);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessSendInviteEmail,
                message,
                new
                {
                    email = request.Email,
                    studioName = studio.StudioName,
                    role = role.ToString(),
                    expiresAt = DateTime.UtcNow.AddMinutes(15)
                }));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/studio-invite/accept
        /// Accept invite link and join studio
        /// Validate:
        /// - Token must be valid and not expired
        /// - User not already member
        /// Action: Add user to studio with specified role
        /// </summary>
        [HttpPost("accept")]
        public async Task<ActionResult<ApiResponse<AcceptStudioInviteResponse>>> AcceptInviteLink(
            [FromBody] AcceptStudioInviteRequest request)
        {
            var userId = ValidateAndGetUserId();

            var inviteData = await _studioInviteService.GetInviteTokenDataAsync(request.Token);
            if (inviteData == null)
            {
                throw new AppException(
                    ErrorCodes.InviteTokenInvalid,
                    StatusCodes.Status400BadRequest);
            }

            var studio = await _studioRepository.GetByIdAsync(inviteData.StudioId);
            if (studio == null)
            {
                throw new AppException(
                    ErrorCodes.StudioNotFound,
                    StatusCodes.Status404NotFound);
            }

            bool isAlreadyMember = await _studioParticipantRepository
                .IsUserInStudioAsync(inviteData.StudioId, userId);

            if (isAlreadyMember)
            {
                throw new AppException(
                    ErrorCodes.StudioAlreadyMember,
                    StatusCodes.Status400BadRequest);
            }

            if (!Enum.TryParse<StudioRole>(inviteData.Role, true, out StudioRole role))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            if (role == StudioRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            // 🔹 ADDED: Determine IsApproved based on studio's IsOpen setting
            bool isApproved = studio.IsOpen;

            var participant = new StudioParticipant
            {
                ParticipantId = Guid.NewGuid(),
                StudioId = inviteData.StudioId,
                UserId = userId,
                Role = role,
                IsApproved = isApproved,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _studioParticipantRepository.AddAsync(participant);

                _logger.LogInformation(
                    "User {UserId} accepted invite for studio {StudioId} with role {Role}, IsApproved={IsApproved}",
                    userId, inviteData.StudioId, role, isApproved);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_StudioParticipants_StudioId_UserId") == true)
            {
                throw new AppException(
                    ErrorCodes.StudioAlreadyMember,
                    StatusCodes.Status400BadRequest);
            }

            var response = new AcceptStudioInviteResponse
            {
                StudioId = studio.StudioId,
                StudioName = studio.StudioName,
                Role = role.ToString(),
                IsApproved = isApproved,
                JoinedAt = participant.CreatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessAcceptInvite);
            return Ok(ApiResponse<AcceptStudioInviteResponse>.Success(
                ErrorCodes.SuccessAcceptInvite,
                message,
                response));
        }
    }
}
