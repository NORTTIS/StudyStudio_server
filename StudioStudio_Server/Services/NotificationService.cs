using StudioStudio_Server.Configurations;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IUserRepository _userRepository;
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouncementService _userAnnouncementService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IUserRepository userRepository,
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _userRepository = userRepository;
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task NotifyTaskAssignedAsync(Guid assigneeId, Guid taskId, Guid assignedBy, string taskTitle, DateTime? deadline)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var assigner = await _userRepository.GetByIdAsync(assignedBy);
            if (assignee == null || assigner == null) return;

            var assignerName = BuildUserName(assigner);
            await CreateInAppAsync(
                assigneeId,
                assignedBy,
                "Task assigned",
                $"{assignerName} assigned you a task: {taskTitle}",
                AnnouncementType.TaskAssignment);

            var taskUrl = BuildTaskUrl(taskId);
            var language = GetLanguage(assignee);
            var body = EmailTemplate.TaskAssignedEmail(taskTitle, assignerName, deadline, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Assigned - Study Studio", body, assignee);
        }

        public async Task NotifyTaskReassignedAsync(Guid newAssigneeId, Guid oldAssigneeId, Guid taskId, Guid reassignedBy, string taskTitle)
        {
            var actor = await _userRepository.GetByIdAsync(reassignedBy);
            var newAssignee = await _userRepository.GetByIdAsync(newAssigneeId);
            var oldAssignee = await _userRepository.GetByIdAsync(oldAssigneeId);
            if (actor == null) return;

            var actorName = BuildUserName(actor);
            var taskUrl = BuildTaskUrl(taskId);

            if (newAssignee != null)
            {
                await CreateInAppAsync(
                    newAssigneeId,
                    reassignedBy,
                    "Task reassigned to you",
                    $"{actorName} reassigned task to you: {taskTitle}",
                    AnnouncementType.TaskReassignment);

                var body = EmailTemplate.TaskReassignedEmail(taskTitle, oldAssignee != null ? BuildUserName(oldAssignee) : "Unassigned", BuildUserName(newAssignee), taskUrl, GetLanguage(newAssignee));
                await _emailService.SendEmailWithPreferenceCheckAsync(newAssignee.Email, "Task Reassigned - Study Studio", body, newAssignee);
            }

            if (oldAssignee != null)
            {
                await CreateInAppAsync(
                    oldAssigneeId,
                    reassignedBy,
                    "Task reassigned",
                    $"{actorName} reassigned your task: {taskTitle}",
                    AnnouncementType.TaskReassignment);

                var body = EmailTemplate.TaskReassignedEmail(taskTitle, BuildUserName(oldAssignee), newAssignee != null ? BuildUserName(newAssignee) : "Unassigned", taskUrl, GetLanguage(oldAssignee));
                await _emailService.SendEmailWithPreferenceCheckAsync(oldAssignee.Email, "Task Reassigned - Study Studio", body, oldAssignee);
            }
        }

        public async Task NotifyTaskStatusChangedAsync(Guid userId, Guid taskId, string oldStatus, string newStatus, string changedBy)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            await CreateInAppAsync(
                userId,
                userId,
                "Task status updated",
                $"{changedBy} changed status: {oldStatus} → {newStatus}",
                AnnouncementType.TaskStatusChange);

            var body = EmailTemplate.TaskStatusChangedEmail("Task", oldStatus, newStatus, changedBy, BuildTaskUrl(taskId), GetLanguage(user));
            await _emailService.SendEmailWithPreferenceCheckAsync(user.Email, "Task Status Updated - Study Studio", body, user);
        }

        public async Task NotifyTaskCompletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid completedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var actor = await _userRepository.GetByIdAsync(completedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            await CreateInAppAsync(
                assigneeId,
                completedBy,
                "Task completed",
                $"{actorName} completed task: {taskTitle}",
                AnnouncementType.TaskCompleted);

            var body = EmailTemplate.TaskCompletedEmail(taskTitle, actorName, BuildTaskUrl(taskId), GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Completed - Study Studio", body, assignee);
        }

        public async Task NotifyMentionedInCommentAsync(Guid mentionedUserId, Guid taskId, string taskTitle, Guid mentionerId, string commentPreview)
        {
            var mentionedUser = await _userRepository.GetByIdAsync(mentionedUserId);
            var mentioner = await _userRepository.GetByIdAsync(mentionerId);
            if (mentionedUser == null || mentioner == null) return;

            var mentionerName = BuildUserName(mentioner);
            var body = EmailTemplate.MentionedInCommentEmail(taskTitle, mentionerName, commentPreview, BuildTaskUrl(taskId), GetLanguage(mentionedUser));
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Task Comment - Study Studio", body, mentionedUser);
        }

        public async Task NotifyMentionedInGroupDiscussAsync(Guid mentionedUserId, Guid groupId, Guid mentionerId, string groupName, string messagePreview)
        {
            var mentionedUser = await _userRepository.GetByIdAsync(mentionedUserId);
            var mentioner = await _userRepository.GetByIdAsync(mentionerId);
            if (mentionedUser == null || mentioner == null) return;

            var mentionerName = BuildUserName(mentioner);
            var body = EmailTemplate.MentionedInGroupDiscussEmail(groupName, mentionerName, messagePreview, BuildGroupDiscussUrl(groupId), GetLanguage(mentionedUser));
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Group Discussion - Study Studio", body, mentionedUser);
        }

        public async Task NotifyTaskDeletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid deletedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var actor = await _userRepository.GetByIdAsync(deletedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            await CreateInAppAsync(
                assigneeId,
                deletedBy,
                "Task deleted",
                $"{actorName} deleted task: {taskTitle}",
                AnnouncementType.TaskDeleted);

            var body = EmailTemplate.TaskDeletedEmail(taskTitle, actorName, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deleted - Study Studio", body, assignee);
        }

        public async Task NotifyTaskUnassignedAsync(Guid previousAssigneeId, Guid taskId, string taskTitle, Guid unassignedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(previousAssigneeId);
            var actor = await _userRepository.GetByIdAsync(unassignedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            await CreateInAppAsync(
                previousAssigneeId,
                unassignedBy,
                "Task unassigned",
                $"{actorName} removed you from task: {taskTitle}",
                AnnouncementType.TaskUnassigned);

            var body = EmailTemplate.TaskUnassignedEmail(taskTitle, actorName, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Unassigned - Study Studio", body, assignee);
        }

        public async Task NotifyTaskOverdueAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            if (assignee == null) return;

            await CreateInAppAsync(
                assigneeId,
                assigneeId,
                "Task overdue",
                $"Task is overdue: {taskTitle}",
                AnnouncementType.TaskOverdue);

            var body = EmailTemplate.TaskOverdueEmail(taskTitle, dueDate, overdueDays, BuildTaskUrl(taskId), GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Overdue - Study Studio", body, assignee);
        }

        public async Task NotifyTaskReminderAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            if (assignee == null) return;

            await CreateInAppAsync(
                assigneeId,
                assigneeId,
                "Task deadline reminder",
                $"Deadline is approaching: {taskTitle}",
                AnnouncementType.TaskReminder);

            var body = EmailTemplate.TaskReminderEmail(taskTitle, dueDate, hoursUntilDeadline, BuildTaskUrl(taskId), GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deadline Reminder - Study Studio", body, assignee);
        }

        private async Task CreateInAppAsync(Guid targetUserId, Guid createdBy, string title, string content, AnnouncementType type)
        {
            var now = DateTime.UtcNow;
            var announcement = new Announcement
            {
                AnnouncementId = Guid.NewGuid(),
                Title = title,
                Content = content,
                Type = type,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now
            };

            await _announcementRepository.AddAsync(announcement);

            await _userAnnouncementService.AddAnnouncementAsync(new UserAnnouncementRequest
            {
                AnnouncementId = announcement.AnnouncementId,
                MentionedId = targetUserId,
                CreatedBy = createdBy,
                IsRead = false,
                CreatedAt = now
            });
        }

        private string BuildTaskUrl(Guid taskId)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            return $"{baseUrl}/tasks/{taskId}";
        }

        private string BuildGroupDiscussUrl(Guid groupId)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            return $"{baseUrl}/group/{groupId}/discuss";
        }

        private static string BuildUserName(User user)
            => string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim()) ? user.Email : $"{user.FirstName} {user.LastName}".Trim();

        private static Language GetLanguage(User user)
            => user.Language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true ? Language.Vietnamese : Language.English;
    }
}
