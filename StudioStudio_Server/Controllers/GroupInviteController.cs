using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.Caches;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    /// <summary>
    /// Controller qu?n l? Group Invites (m?i thành viên vào group)
    /// Route: /api/invite
    /// </summary>
    [Route("api/invite")]
    [ApiController]
    [Authorize]
    public class GroupInviteController : ControllerBase
    {
        private readonly IGroupInviteService _groupInviteService;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
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
            _userSubscriptionRepository = userSubscriptionRepository;
            _emailService = emailService;
            _userRepository = userRepository;
            _messageService = messageService;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Xác th?c và l?y userId t? JWT token
        /// Validate: User không ðý?c là admin
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
        /// Validate và parse GroupRole t? string
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
        /// Validate user có quy?n t?o invite (Owner ho?c Moderator)
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
        /// Validate role Moderator (ch? có th? có 1 Moderator)
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
        /// T?o invite link cho group
        /// Validate:
        /// - User ph?i là Owner ho?c Moderator
        /// - Role h?p l? (không ðý?c là Owner)
        /// - Không vý?t quá rate limit (5 links/15 phút)
        /// - Không th? t?o Moderator invite n?u ð? có Moderator
        /// Expiry: 15 phút
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
        /// G?i email invite cho group
        /// Validate: Týõng t? CreateInviteLink
        /// Action: T?o token và g?i email v?i link invite
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

            await _emailService.SendLinkAsync(request.Email, subject, body);

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
        /// Accept invite link và join group
        /// Validate:
        /// - Token ph?i h?p l? và chýa expire
        /// - Group ph?i t?n t?i
        /// - User chýa là member
        /// - Không vý?t quá member limit
        /// - Role Moderator ch? có th? có 1
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

            var participant = new GroupParticipant
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = inviteData.GroupId,
                UserId = userId,
                Role = role,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _groupParticipantRepository.AddAsync(participant);

                _logger.LogInformation(
                    "User {UserId} accepted invite and joined group {GroupId} with role {Role}",
                    userId, inviteData.GroupId, role);
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
