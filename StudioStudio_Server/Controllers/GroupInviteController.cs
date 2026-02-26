using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Configurations;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;
using System.Security.Claims;

namespace StudioStudio_Server.Controllers
{
    [Route("api/invite")]
    [ApiController]
    public class GroupInviteController : ControllerBase
    {
        private readonly ILogger<GroupInviteController> _logger;
        private readonly IMessageService _messageService;
        private readonly IGroupInviteService _groupInviteService;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly IUserSubscriptionRepository _userSubscriptionRepository;
        private readonly IEmailService _emailService;
        private readonly IUserRepository _userRepository;
        private readonly IConfiguration _configuration;

        public GroupInviteController(
            ILogger<GroupInviteController> logger,
            IMessageService messageService,
            IGroupInviteService groupInviteService,
            IGroupRepository groupRepository,
            IGroupParticipantRepository groupParticipantRepository,
            IUserSubscriptionRepository userSubscriptionRepository,
            IEmailService emailService,
            IUserRepository userRepository,
            IConfiguration configuration)
        {
            _logger = logger;
            _messageService = messageService;
            _groupInviteService = groupInviteService;
            _groupRepository = groupRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _userSubscriptionRepository = userSubscriptionRepository;
            _emailService = emailService;
            _userRepository = userRepository;
            _configuration = configuration;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<CreateInviteLinkResponse>>> CreateInviteLink(
            [FromBody] CreateInviteLinkRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Validate role
            if (!Enum.TryParse<GroupRole>(request.Role, true, out GroupRole role))
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Cannot create invite link with Owner role
            if (role == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Check if group exists and is active
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // ? Check if trying to create Moderator invite when Moderator already exists
            if (role == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository.GetRoleCountByGroupIdAsync(request.GroupId, GroupRole.Moderator);
                if (moderatorCount > 0)
                {
                    _logger.LogWarning(
                        "Attempt to create Moderator invite for group {GroupId} that already has a Moderator. Request by user {UserId}",
                        request.GroupId, userId);
                    throw new AppException(ErrorCodes.GroupOnlyOneModerator, StatusCodes.Status400BadRequest);
                }
            }

            // Check rate limit
            bool canCreate = await _groupInviteService.CheckInviteCreationRateLimitAsync(request.GroupId, userId);
            if (!canCreate)
            {
                throw new AppException(ErrorCodes.InviteRateLimitExceeded, StatusCodes.Status429TooManyRequests);
            }

            // Generate token
            string token = await _groupInviteService.GenerateInviteTokenAsync();

            // Prepare invite data
            var inviteData = new GroupInviteToken
            {
                GroupId = request.GroupId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            // Store in Redis
            bool stored = await _groupInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(ErrorCodes.UnexpectedError, StatusCodes.Status500InternalServerError);
            }

            // Build invite URL
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
                "Invite link created for group {GroupId} with role {Role} by user {UserId}. Token expires at {ExpiresAt}",
                request.GroupId, role, userId, response.ExpiresAt);

            var message = _messageService.GetMessage(ErrorCodes.SuccessCreateInvite);
            return Ok(ApiResponse<CreateInviteLinkResponse>.Success(ErrorCodes.SuccessCreateInvite, message, response));
        }

        [HttpPost("email")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> SendInviteEmail(
            [FromBody] SendInviteEmailRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Validate role
            if (!Enum.TryParse<GroupRole>(request.Role, true, out GroupRole role))
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Cannot create invite link with Owner role
            if (role == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Check if group exists and is active
            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is Owner or Moderator
            var userParticipant = await _groupParticipantRepository.GetByGroupAndUserAsync(request.GroupId, userId);
            if (userParticipant == null ||
                (userParticipant.Role != GroupRole.Owner && userParticipant.Role != GroupRole.Moderator))
            {
                throw new AppException(ErrorCodes.GroupPermissionDenied, StatusCodes.Status403Forbidden);
            }

            // ? Check if trying to invite Moderator when Moderator already exists
            if (role == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository.GetRoleCountByGroupIdAsync(request.GroupId, GroupRole.Moderator);
                if (moderatorCount > 0)
                {
                    _logger.LogWarning(
                        "Attempt to send Moderator invite for group {GroupId} that already has a Moderator. Request by user {UserId}",
                        request.GroupId, userId);
                    throw new AppException(ErrorCodes.GroupOnlyOneModerator, StatusCodes.Status400BadRequest);
                }
            }

            // Check rate limit
            bool canCreate = await _groupInviteService.CheckInviteCreationRateLimitAsync(request.GroupId, userId);
            if (!canCreate)
            {
                throw new AppException(ErrorCodes.InviteRateLimitExceeded, StatusCodes.Status429TooManyRequests);
            }

            // Get inviter info
            var inviter = await _userRepository.GetByIdAsync(userId);
            if (inviter == null)
            {
                throw new AppException(ErrorCodes.UserNotFound, StatusCodes.Status404NotFound);
            }

            // Generate token
            string token = await _groupInviteService.GenerateInviteTokenAsync();

            // Prepare invite data
            var inviteData = new GroupInviteToken
            {
                GroupId = request.GroupId,
                Role = role.ToString(),
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            // Store in Redis
            bool stored = await _groupInviteService.StoreInviteTokenAsync(token, inviteData);
            if (!stored)
            {
                throw new AppException(ErrorCodes.UnexpectedError, StatusCodes.Status500InternalServerError);
            }

            // Build invite URL
            string frontendUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            string inviteUrl = $"{frontendUrl}/invite/{token}";

            // Prepare email using template
            string inviterName = $"{inviter.FirstName} {inviter.LastName}";
            string subject = $"Invitation to join {group.GroupName} on Study Studio";
            string body = EmailTemplate.GroupInviteEmail(
                inviteUrl,
                inviterName,
                group.GroupName,
                role.ToString(),
                group.Description
            );

            // Send email
            await _emailService.SendLinkAsync(request.Email, subject, body);

            _logger.LogInformation(
                "Invite email sent to {Email} for group {GroupId} with role {Role} by user {UserId}",
                request.Email, request.GroupId, role, userId);

            var message = _messageService.GetMessage(ErrorCodes.SuccessSendInviteEmail);
            return Ok(ApiResponse<object>.Success(ErrorCodes.SuccessSendInviteEmail, message, new
            {
                email = request.Email,
                groupName = group.GroupName,
                role = role.ToString(),
                expiresAt = DateTime.UtcNow.AddMinutes(15)
            }));
        }

        [HttpPost("accept")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<AcceptInviteLinkResponse>>> AcceptInviteLink(
            [FromBody] AcceptInviteLinkRequest request)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out Guid userId))
            {
                throw new AppException(ErrorCodes.AuthInvalidCredential, StatusCodes.Status401Unauthorized);
            }

            var isAdminClaim = User.FindFirst("IsAdmin")?.Value;
            var isAdmin = isAdminClaim != null && bool.TryParse(isAdminClaim, out var adminResult) && adminResult;
            if (isAdmin)
            {
                throw new AppException(ErrorCodes.AuthForbidden, StatusCodes.Status403Forbidden);
            }

            // Get invite data from Redis
            var inviteData = await _groupInviteService.GetInviteTokenDataAsync(request.Token);
            if (inviteData == null)
            {
                throw new AppException(ErrorCodes.InviteTokenInvalid, StatusCodes.Status400BadRequest);
            }

            // Validate group exists and is active
            var group = await _groupRepository.GetByIdAsync(inviteData.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.GroupNotFound, StatusCodes.Status404NotFound);
            }

            // Check if user is already a member
            bool isAlreadyMember = await _groupParticipantRepository.IsUserInGroupAsync(inviteData.GroupId, userId);
            if (isAlreadyMember)
            {
                throw new AppException(ErrorCodes.GroupAlreadyMember, StatusCodes.Status400BadRequest);
            }

            // Parse role
            if (!Enum.TryParse<GroupRole>(inviteData.Role, true, out GroupRole role))
            {
                throw new AppException(ErrorCodes.InviteInvalidRole, StatusCodes.Status400BadRequest);
            }

            // Only 1 Owner and 1 Moderator allowed per group
            if (role == GroupRole.Owner)
            {
                throw new AppException(ErrorCodes.GroupOnlyOneOwner, StatusCodes.Status400BadRequest);
            }
            else if (role == GroupRole.Moderator)
            {
                int moderatorCount = await _groupParticipantRepository.GetRoleCountByGroupIdAsync(inviteData.GroupId, GroupRole.Moderator);
                if (moderatorCount > 0)
                {
                    _logger.LogWarning(
                        "Attempt to add second Moderator to group {GroupId}. Invite rejected for user {UserId}",
                        inviteData.GroupId, userId);
                    throw new AppException(ErrorCodes.GroupOnlyOneModerator, StatusCodes.Status400BadRequest);
                }
            }

            // Get group owner's subscription plan
            var ownerParticipant = group.Participants.FirstOrDefault(p => p.Role == GroupRole.Owner);
            if (ownerParticipant == null)
            {
                throw new AppException(ErrorCodes.UnexpectedError, StatusCodes.Status500InternalServerError);
            }

            var subscriptionPlan = await _userSubscriptionRepository.GetSubscriptionPlanByUserIdAsync(ownerParticipant.UserId);
            int memberLimit = subscriptionPlan?.MaxMembersPerGroup ?? 10;

            // Check current member count
            int currentMemberCount = await _groupParticipantRepository.GetParticipantCountByGroupIdAsync(inviteData.GroupId);
            if (currentMemberCount >= memberLimit)
            {
                throw new AppException(ErrorCodes.GroupMemberLimitReached, StatusCodes.Status403Forbidden);
            }

            // Add user as participant
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
                // Handle race condition - user already added
                throw new AppException(ErrorCodes.GroupAlreadyMember, StatusCodes.Status400BadRequest);
            }

            var response = new AcceptInviteLinkResponse
            {
                GroupId = group.GroupId,
                GroupName = group.GroupName,
                Role = role.ToString(),
                JoinedAt = participant.CreatedAt
            };

            var message = _messageService.GetMessage(ErrorCodes.SuccessAcceptInvite);
            return Ok(ApiResponse<AcceptInviteLinkResponse>.Success(ErrorCodes.SuccessAcceptInvite, message, response));
        }
    }
}
