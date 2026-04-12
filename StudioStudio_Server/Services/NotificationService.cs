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
        private readonly IAnnouncementRepository _announcementRepository;
        private readonly IUserAnnouncementService _userAnnouncementService;
        private readonly IEmailService _emailService;
        private readonly ITaskRepository _taskRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IEmailService emailService,
            ITaskRepository taskRepository,
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _emailService = emailService;
            _taskRepository = taskRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task NotifyTaskAssignedAsync(User assignee, User assignedBy, Guid taskId, string taskTitle, DateTime? deadline)
        {
            var assignerName = BuildUserName(assignedBy);
            var groupIdForAssign = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                assignedBy.UserId,
                "Task assigned",
                $"{assignerName} assigned you a task: {taskTitle}",
                AnnouncementType.TaskAssignment,
                taskId,
                groupIdForAssign,
                "task");

            var taskUrl = groupIdForAssign.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskAssignedEmail(taskTitle, assignerName, deadline, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Assigned - Study Studio", body, assignee);
        }

        public async Task NotifyTaskReassignedAsync(User newAssignee, User oldAssignee, User actor, Guid taskId, string taskTitle)
        {
            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);

            var newAssigneeTaskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(newAssignee)) : "";
            await CreateInAppAsync(
                newAssignee.UserId,
                actor.UserId,
                "Task reassigned to you",
                $"{actorName} reassigned task to you: {taskTitle}",
                AnnouncementType.TaskReassignment,
                taskId,
                groupId,
                "task");

            var body = EmailTemplate.TaskReassignedEmail(
                taskTitle,
                BuildUserName(oldAssignee),
                BuildUserName(newAssignee),
                newAssigneeTaskUrl,
                GetLanguage(newAssignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(newAssignee.Email, "Task Reassigned - Study Studio", body, newAssignee);

            var oldAssigneeTaskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(oldAssignee)) : "";
            await CreateInAppAsync(
                oldAssignee.UserId,
                actor.UserId,
                "Task reassigned",
                $"{actorName} reassigned your task: {taskTitle}",
                AnnouncementType.TaskReassignment,
                taskId,
                groupId,
                "task");

            var body2 = EmailTemplate.TaskReassignedEmail(
                taskTitle,
                BuildUserName(oldAssignee),
                BuildUserName(newAssignee),
                oldAssigneeTaskUrl,
                GetLanguage(oldAssignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(oldAssignee.Email, "Task Reassigned - Study Studio", body2, oldAssignee);
        }

        public async Task NotifyTaskStatusChangedAsync(User user, Guid taskId, string oldStatus, string newStatus, string changedBy)
        {
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(user);

            await CreateInAppAsync(
                user.UserId,
                user.UserId,
                "Task status updated",
                $"{changedBy} changed status: {oldStatus} → {newStatus}",
                AnnouncementType.TaskStatusChange,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskStatusChangedEmail("Task", oldStatus, newStatus, changedBy, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(user.Email, "Task Status Updated - Study Studio", body, user);
        }

        public async Task NotifyTaskCompletedAsync(User assignee, User completedBy, Guid taskId, string taskTitle)
        {
            var actorName = BuildUserName(completedBy);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                completedBy.UserId,
                "Task completed",
                $"{actorName} completed task: {taskTitle}",
                AnnouncementType.TaskCompleted,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskCompletedEmail(taskTitle, actorName, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Completed - Study Studio", body, assignee);
        }

        public async Task NotifyMentionedInCommentAsync(User mentionedUser, User mentioner, Guid taskId, string taskTitle, string commentPreview)
        {
            var mentionerName = BuildUserName(mentioner);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(mentionedUser);

            await CreateInAppAsync(
                mentionedUser.UserId,
                mentioner.UserId,
                "Mentioned in comment",
                $"{mentionerName} mentioned you in a comment: {taskTitle}",
                AnnouncementType.Mention,
                taskId,
                groupId,
                "comment");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.MentionedInCommentEmail(taskTitle, mentionerName, commentPreview, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Task Comment - Study Studio", body, mentionedUser);
        }

        public async Task NotifyMentionedInGroupDiscussAsync(User mentionedUser, User mentioner, Guid groupId, string groupName, string messagePreview)
        {
            var mentionerName = BuildUserName(mentioner);
            var language = GetLanguage(mentionedUser);

            await CreateInAppAsync(
                mentionedUser.UserId,
                mentioner.UserId,
                "Mentioned in group discussion",
                $"{mentionerName} mentioned you in {groupName}",
                AnnouncementType.Mention,
                null,
                groupId,
                "discuss");

            var body = EmailTemplate.MentionedInGroupDiscussEmail(groupName, mentionerName, messagePreview, BuildGroupDiscussUrl(groupId), language);
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Group Discussion - Study Studio", body, mentionedUser);
        }

        public async Task NotifyTaskDeletedAsync(User assignee, User deletedBy, Guid taskId, string taskTitle)
        {
            var actorName = BuildUserName(deletedBy);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                deletedBy.UserId,
                "Task deleted",
                $"{actorName} deleted task: {taskTitle}",
                AnnouncementType.TaskDeleted,
                taskId,
                groupId,
                "task");

            var body = EmailTemplate.TaskDeletedEmail(taskTitle, actorName, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deleted - Study Studio", body, assignee);
        }

        public async Task NotifyTaskUnassignedAsync(User assignee, User actor, Guid taskId, string taskTitle)
        {
            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                actor.UserId,
                "Task unassigned",
                $"{actorName} removed you from task: {taskTitle}",
                AnnouncementType.TaskUnassigned,
                taskId,
                groupId,
                "task");

            var body = EmailTemplate.TaskUnassignedEmail(taskTitle, actorName, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Unassigned - Study Studio", body, assignee);
        }

        public async Task NotifyTaskOverdueAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays)
        {
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                assignee.UserId,
                "Task overdue",
                $"Task is overdue: {taskTitle}",
                AnnouncementType.TaskOverdue,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskOverdueEmail(taskTitle, dueDate, overdueDays, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Overdue - Study Studio", body, assignee);
        }

        public async Task NotifyTaskReminderAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline)
        {
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            await CreateInAppAsync(
                assignee.UserId,
                assignee.UserId,
                "Task deadline reminder",
                $"Deadline is approaching: {taskTitle}",
                AnnouncementType.TaskReminder,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskReminderEmail(taskTitle, dueDate, hoursUntilDeadline, taskUrl, language);
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deadline Reminder - Study Studio", body, assignee);
        }

        private async Task CreateInAppAsync(
            Guid targetUserId,
            Guid createdBy,
            string title,
            string content,
            AnnouncementType type,
            Guid? taskId = null,
            Guid? groupId = null,
            string? sourceType = null)
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
                PublishedAt = now,
                TaskId = taskId,
                GroupId = groupId,
                SourceType = sourceType
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

        private string BuildTaskUrl(Guid taskId, Language language)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var locale = language == Language.Vietnamese ? "vi" : "en";
            return $"{baseUrl}/{locale}/group/task/{taskId}";
        }

        private async Task<Guid?> _getGroupIdForTaskAsync(Guid taskId)
        {
            var task = await _taskRepository.GetByIdAsync(taskId);
            return task?.GroupId;
        }

        private string BuildGroupDiscussUrl(Guid groupId)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            return $"{baseUrl}/vi/group/{groupId}/discuss";
        }

        private static string BuildUserName(User user)
            => string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim()) ? user.Email : $"{user.FirstName} {user.LastName}".Trim();

        private static Language GetLanguage(User user)
            => user.Language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true ? Language.Vietnamese : Language.English;
    }
}
