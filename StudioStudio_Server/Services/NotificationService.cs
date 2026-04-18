using StudioStudio_Server.Configurations;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class NotificationService(
        IAnnouncementRepository announcementRepository,
        IUserAnnouncementService userAnnouncementService,
        IEmailService emailService,
        ITaskRepository taskRepository,
        IConfiguration configuration,
        ILogger<NotificationService> logger) : INotificationService
    {
        private readonly ILogger<NotificationService> _logger = logger;

        /// <summary>
        /// Notify the assignee about a newly assigned task.
        /// Validate: assignee and assignedBy must be loaded user entities and taskId must resolve to a task.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskAssignedAsync(User assignee, User assignedBy, Guid taskId, string taskTitle, DateTime? deadline, CancellationToken cancellationToken = default)
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
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Assigned - Study Studio", body, assignee);
        }

        /// <summary>
        /// Notify the old and new assignees when a task is reassigned.
        /// Validate: assignees and actor must be loaded user entities and taskId must resolve to a task.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskReassignedAsync(User newAssignee, User oldAssignee, User actor, Guid taskId, string taskTitle, CancellationToken cancellationToken = default)
        {
            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            

            if (newAssignee.UserId != actor.UserId)
            {
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
                await emailService.SendEmailWithPreferenceCheckAsync(newAssignee.Email, "Task Reassigned - Study Studio", body, newAssignee);
            }
            //Check old member is deleted or not, if deleted, only notify new member
            bool isOldAssigneeDeleted = oldAssignee.Status == UserStatus.Deleted;
            if (oldAssignee.UserId != actor.UserId && !isOldAssigneeDeleted)
            {
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
                await emailService.SendEmailWithPreferenceCheckAsync(oldAssignee.Email, "Task Reassigned - Study Studio", body2, oldAssignee);
            }
        }

        /// <summary>
        /// Notify the assignee when a task status changes.
        /// Validate: user and actor must be loaded user entities and status values must be non-empty.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskStatusChangedAsync(User user, User actor, Guid taskId, string oldStatus, string newStatus, string changedBy, CancellationToken cancellationToken = default)
        {
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(user);
            //check meber is deleted or not, if deleted, only notify new member
            bool isUserDeleted = user.Status == UserStatus.Deleted;
            if (isUserDeleted)
            {
                return;
            }
            await CreateInAppAsync(
                user.UserId,
                actor.UserId,
                "Task status updated",
                $"{changedBy} changed status: {oldStatus} → {newStatus}",
                AnnouncementType.TaskStatusChange,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskStatusChangedEmail("Task", oldStatus, newStatus, changedBy, taskUrl, language);
            await emailService.SendEmailWithPreferenceCheckAsync(user.Email, "Task Status Updated - Study Studio", body, user);
        }

        /// <summary>
        /// Notify the assignee that a task has been completed.
        /// Validate: assignee and completedBy must be loaded user entities and taskId must resolve to a task.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskCompletedAsync(User assignee, User completedBy, Guid taskId, string taskTitle, CancellationToken cancellationToken = default)
        {
            var actorName = BuildUserName(completedBy);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            var language = GetLanguage(assignee);

            // Check if the assignee is deleted
            if (assignee.Status == UserStatus.Deleted)
            {
                return;
            }

            await CreateInAppAsync(
                assignee.UserId,
                assignee.UserId,
                "Task completed",
                $"{actorName} completed task: {taskTitle}",
                AnnouncementType.TaskCompleted,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, language) : "";
            var body = EmailTemplate.TaskCompletedEmail(taskTitle, actorName, taskUrl, language);
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Completed - Study Studio", body, assignee);
        }

        /// <summary>
        /// Notify the assignee when a task is deleted.
        /// Validate: assignee and deletedBy must be loaded user entities and taskId must resolve to a task.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskDeletedAsync(User assignee, User deletedBy, Guid taskId, string taskTitle, CancellationToken cancellationToken = default)
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
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deleted - Study Studio", body, assignee);
        }

        /// <summary>
        /// Notify the assignee when they are removed from a task.
        /// Validate: assignee and actor must be loaded user entities and taskId must resolve to a task.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskUnassignedAsync(User assignee, User actor, Guid taskId, string taskTitle, CancellationToken cancellationToken = default)
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
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Unassigned - Study Studio", body, assignee);
        }

        /// <summary>
        /// Notify the assignee when a task is overdue.
        /// Validate: assignee must be a loaded user entity and dueDate must represent the overdue deadline.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskOverdueAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays, CancellationToken cancellationToken = default)
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
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Overdue - Study Studio", body, assignee);
        }

        /// <summary>
        /// Notify the assignee about an approaching task deadline.
        /// Validate: assignee must be a loaded user entity and dueDate must represent the deadline.
        /// Returns: A completed notification task after in-app and email delivery attempts.
        /// </summary>
        public async Task NotifyTaskReminderAsync(User assignee, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline, CancellationToken cancellationToken = default)
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
            await emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deadline Reminder - Study Studio", body, assignee);
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

            await announcementRepository.AddAsync(announcement);

            await userAnnouncementService.AddAnnouncementAsync(new UserAnnouncementRequest
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
            var baseUrl = configuration["Frontend:BaseUrl"] ?? "http://localhost:3000";
            var locale = language == Language.Vietnamese ? "vi" : "en";
            return $"{baseUrl}/{locale}/group/task/{taskId}";
        }

        private async Task<Guid?> _getGroupIdForTaskAsync(Guid taskId)
        {
            var task = await taskRepository.GetByIdAsync(taskId);
            return task?.GroupId;
        }

        private static string BuildUserName(User user)
            => string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim()) ? user.Email : $"{user.FirstName} {user.LastName}".Trim();

        private static Language GetLanguage(User user)
            => user.Language?.Equals("vi", StringComparison.OrdinalIgnoreCase) == true ? Language.Vietnamese : Language.English;
    }
}
