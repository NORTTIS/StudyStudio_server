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
    /// Controller for managing Group Invitations
    /// Route: /api/invite
    /// Uses Redis for token storage (15 min expiry)
    /// </summary>
    [Route("api/invite")]
    [ApiController]
    [Authorize]
    public class GroupInviteController : ControllerBase
    {
        private readonly IGroupInviteService _groupInviteService;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IStudioParticipantRepository _studioParticipantRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IMessageService _messageService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<GroupInviteController> _logger;

        public GroupInviteController(
            IGroupInviteService groupInviteService,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IStudioParticipantRepository studioParticipantRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            IMessageService messageService,
            IConfiguration configuration,
            ILogger<GroupInviteController> logger)
        {
            _groupInviteService = groupInviteService;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _studioParticipantRepository = studioParticipantRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
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
        /// Validate and parse role string to GroupRole enum
        /// Validate: Role must not be Owner
        /// </summary>
        private GroupRole ValidateAndParseRole(string roleString)
        {
            if (!Enum.TryParse<GroupRole>(roleString, true, out GroupRole role))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            if (role == GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            return role;
        }

        /// <summary>
        /// Validate user has permission to create invite (Owner or Moderator)
        /// </summary>
        private async Task ValidateInvitePermissionAsync(Guid groupId, Guid userId)
        {
            var userParticipant = await _groupParticipantRepository
                .GetByGroupAndUserAsync(groupId, userId);

            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner &&
                 userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(
                    ErrorCodes.GroupPermissionDenied,
                    StatusCodes.Status403Forbidden);
            }
        }

        /// <summary>
        /// Validate Moderator role (only 1 Moderator allowed)
        /// </summary>
        private async Task ValidateModeratorRoleAsync(Guid groupId, GroupRole role)
        {
            if (role == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository
                    .GetRoleCountByGroupIdAsync(groupId, GroupRole.Moderator);

                if (moderatorCount > 0)
                {
                    _logger.LogWarning(
                        "Attempt to invite Moderator for group {GroupId} that already has a Moderator",
                        groupId);

                    throw new AppException(
                        ErrorCodes.GroupOnlyOneModerator,
                        StatusCodes.Status400BadRequest);
                }
            }
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/invite/create
        /// Create invite link for group
        /// Validate:
        /// - User must be Owner or Moderator
        /// - Role must be valid (cannot be Owner)
        /// - Must not exceed rate limit (5 links/15 minutes)
        /// - Cannot create Moderator invite if Moderator already exists
        /// Expiry: 15 minutes
        /// </summary>
        [HttpPost("create")]
        public async Task<ActionResult<ApiResponse<CreateInviteLinkResponse>>> CreateInviteLink(
            [FromBody] CreateInviteLinkRequest request)
        {
            var userId = ValidateAndGetUserId();
            var role = ValidateAndParseRole(request.Role);

            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            await ValidateInvitePermissionAsync(request.GroupId, userId);
            await ValidateModeratorRoleAsync(request.GroupId, role);

            bool canCreate = await _groupInviteService
                .CheckInviteCreationRateLimitAsync(request.GroupId, userId);

            if (!canCreate)
            {
                throw new AppException(
                    ErrorCodes.InviteRateLimitExceeded,
                    StatusCodes.Status429TooManyRequests);
            }

            string token = await _groupInviteService.GenerateInviteTokenAsync();

            var inviteData = new GroupInviteToken
            {
                GroupId = request.GroupId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            bool stored = await _groupInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }

            string frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            string inviteUrl = $"{frontendUrl}/invite/{token}";

            var response = new CreateInviteLinkResponse
            {
                InviteUrl = inviteUrl,
                Token = token,
                Role = role.ToString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = inviteData.CreatedAt
            };

            _logger.LogInformation(
                "Invite link created for group {GroupId} with role {Role} by user {UserId}",
                request.GroupId, role, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateInvite);
            return Ok(ApiResponse<CreateInviteLinkResponse>.Success(
                ErrorCodes.SuccessCreateInvite,
                message,
                response));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/invite/email
        /// Send invite link via email
        /// Validate:
        /// - User must be Owner or Moderator
        /// - Token must be valid
        /// - Email must be valid format
        /// Action: Send email with invite link
        /// </summary>
        [HttpPost("email")]
        public async Task<ActionResult<ApiResponse<object>>> SendInviteEmail(
            [FromBody] SendInviteEmailRequest request)
        {
            var userId = ValidateAndGetUserId();
            var role = ValidateAndParseRole(request.Role);

            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            await ValidateInvitePermissionAsync(request.GroupId, userId);
            await ValidateModeratorRoleAsync(request.GroupId, role);

            bool canCreate = await _groupInviteService
                .CheckInviteCreationRateLimitAsync(request.GroupId, userId);

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

            string token = await _groupInviteService.GenerateInviteTokenAsync();

            var inviteData = new GroupInviteToken
            {
                GroupId = request.GroupId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            bool stored = await _groupInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }

            string frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            string inviteUrl = $"{frontendUrl}/invite/{token}";

            string inviterName = $"{inviter.FirstName} {inviter.LastName}";
            string subject = $"Invitation to join {group.GroupName} on Study Studio";
            string body = EmailTemplate.GroupInviteEmail(
                inviteUrl,
                inviterName,
                group.GroupName,
                role.ToString(),
                group.Description);

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
                "Invite email sent to {Email} for group {GroupId} with role {Role} by user {UserId}",
                request.Email, request.GroupId, role, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessSendInviteEmail);
            return Ok(ApiResponse<object>.Success(
                ErrorCodes.SuccessSendInviteEmail,
                message,
                new
                {
                    email = request.Email,
                    groupName = group.GroupName,
                    role = role.ToString(),
                    expiresAt = DateTime.UtcNow.AddMinutes(15)
                }));
        }

        /// <summary>
        /// [AUTHORIZED] POST /api/invite/accept
        /// Accept invite link and join group
        /// Validate:
        /// - Token must be valid and not expired
        /// - User not already member
        /// - Group member limit not exceeded
        /// - Only 1 Moderator allowed
        /// Action: Add user to group with specified role
        /// </summary>
        [HttpPost("accept")]
        public async Task<ActionResult<ApiResponse<AcceptInviteLinkResponse>>> AcceptInviteLink(
            [FromBody] AcceptInviteLinkRequest request)
        {
            var userId = ValidateAndGetUserId();

            var inviteData = await _groupInviteService.GetInviteTokenDataAsync(request.Token);
            if (inviteData == null)
            {
                throw new AppException(
                    ErrorCodes.InviteTokenInvalid,
                    StatusCodes.Status400BadRequest);
            }

            var group = await _groupRepository.GetByIdAsync(inviteData.GroupId);
            if (group == null)
            {
                throw new AppException(
                    ErrorCodes.GroupNotFound,
                    StatusCodes.Status404NotFound);
            }

            bool isAlreadyMember = await _groupParticipantRepository
                .IsUserInGroupAsync(inviteData.GroupId, userId);

            if (isAlreadyMember)
            {
                throw new AppException(
                    ErrorCodes.GroupAlreadyMember,
                    StatusCodes.Status400BadRequest);
            }

            if (!Enum.TryParse<GroupRole>(inviteData.Role, true, out GroupRole role))
            {
                throw new AppException(
                    ErrorCodes.InviteInvalidRole,
                    StatusCodes.Status400BadRequest);
            }

            if (role == GroupRole.Owner)
            {
                throw new AppException(
                    ErrorCodes.GroupOnlyOneOwner,
                    StatusCodes.Status400BadRequest);
            }
            else if (role == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository
                    .GetRoleCountByGroupIdAsync(inviteData.GroupId, GroupRole.Moderator);

                if (moderatorCount > 0)
                {
                    _logger.LogWarning(
                        "Attempt to add second Moderator to group {GroupId}. Invite rejected for user {UserId}",
                        inviteData.GroupId, userId);

                    throw new AppException(
                        ErrorCodes.GroupOnlyOneModerator,
                        StatusCodes.Status400BadRequest);
                }
            }

            var ownerParticipant = group.Participants
                .FirstOrDefault(p => p.Role == GroupRole.Owner);

            if (ownerParticipant == null)
            {
                throw new AppException(
                    ErrorCodes.UnexpectedError,
                    StatusCodes.Status500InternalServerError);
            }

            var subscriptionPlan = await _userSubscriptionRepository
                .GetSubscriptionPlanByUserIdAsync(ownerParticipant.UserId);

            int memberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10;
            int currentMemberCount = await _groupParticipantRepository
                .GetParticipantCountByGroupIdAsync(inviteData.GroupId);

            if (currentMemberCount >= memberLimit)
            {
                throw new AppException(
                    ErrorCodes.GroupMemberLimitReached,
                    StatusCodes.Status403Forbidden);
            }

            // 🔹 ADDED: Determine IsApproved based on group's IsOpen setting
            bool isApproved = group.IsOpen;

            var participant = new GroupParticipant
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = inviteData.GroupId,
                UserId = userId,
                Role = role,
                IsApproved = isApproved,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _groupParticipantRepository.AddAsync(participant);

                // 🔹 ADDED: Auto-add user to studio if group belongs to a studio and user is not already a member
                if (group.StudioId.HasValue)
                {
                    var isAlreadyStudioMember = await _studioParticipantRepository
                        .IsUserApprovedInStudioAsync(group.StudioId.Value, userId);

                    if (!isAlreadyStudioMember)
                    {
                        var studioParticipant = new StudioParticipant
                        {
                            ParticipantId = Guid.NewGuid(),
                            StudioId = group.StudioId.Value,
                            UserId = userId,
                            Role = StudioRole.Member,
                            IsApproved = isApproved,
                            CreatedAt = DateTime.UtcNow
                        };

                        try
                        {
                            await _studioParticipantRepository.AddAsync(studioParticipant);

                            _logger.LogInformation(
                                "User {UserId} auto-added to studio {StudioId} via group {GroupId} join, IsApproved={IsApproved}",
                                userId, group.StudioId.Value, inviteData.GroupId, isApproved);
                        }
                        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_StudioParticipants_StudioId_UserId") == true)
                        {
                            // User already in studio (duplicate), ignore
                            _logger.LogWarning(
                                "User {UserId} already exists in studio {StudioId} when joining group {GroupId}",
                                userId, group.StudioId.Value, inviteData.GroupId);
                        }
                    }
                }

                _logger.LogInformation(
                    "User {UserId} accepted invite for group {GroupId} with role {Role}, IsApproved={IsApproved}",
                    userId, inviteData.GroupId, role, isApproved);
            }
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_GroupParticipants_GroupId_UserId") == true)
            {
                throw new AppException(
                    ErrorCodes.GroupAlreadyMember,
                    StatusCodes.Status400BadRequest);
            }

            var response = new AcceptInviteLinkResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Role = role.ToString(),
                IsApproved = isApproved,
                JoinedAt = participant.CreatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessAcceptInvite);
            return Ok(ApiResponse<AcceptInviteLinkResponse>.Success(
                ErrorCodes.SuccessAcceptInvite,
                message,
                response));
        }
    }
}
