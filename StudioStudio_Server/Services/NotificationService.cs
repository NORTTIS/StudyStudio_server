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
        private readonly ITaskRepository _taskRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IUserRepository userRepository,
            IAnnouncementRepository announcementRepository,
            IUserAnnouncementService userAnnouncementService,
            IEmailService emailService,
            ITaskRepository taskRepository,
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _userRepository = userRepository;
            _announcementRepository = announcementRepository;
            _userAnnouncementService = userAnnouncementService;
            _emailService = emailService;
            _taskRepository = taskRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task NotifyTaskAssignedAsync(Guid assigneeId, Guid taskId, Guid assignedBy, string taskTitle, DateTime? deadline)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var assigner = await _userRepository.GetByIdAsync(assignedBy);
            if (assignee == null || assigner == null) return;

            var assignerName = BuildUserName(assigner);
            var groupIdForAssign = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                assigneeId,
                assignedBy,
                "Task assigned",
                $"{assignerName} assigned you a task: {taskTitle}",
                AnnouncementType.TaskAssignment,
                taskId,
                groupIdForAssign,
                "task");

            var language = GetLanguage(assignee);
            var taskUrl = groupIdForAssign.HasValue ? BuildTaskUrl(taskId, language) : "";
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
            var groupId = await _getGroupIdForTaskAsync(taskId);

            if (newAssignee != null)
            {
                var newAssigneeTaskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(newAssignee)) : "";
                await CreateInAppAsync(
                    newAssigneeId,
                    reassignedBy,
                    "Task reassigned to you",
                    $"{actorName} reassigned task to you: {taskTitle}",
                    AnnouncementType.TaskReassignment,
                    taskId,
                    groupId,
                    "task");

                var body = EmailTemplate.TaskReassignedEmail(taskTitle, oldAssignee != null ? BuildUserName(oldAssignee) : "Unassigned", BuildUserName(newAssignee), newAssigneeTaskUrl, GetLanguage(newAssignee));
                await _emailService.SendEmailWithPreferenceCheckAsync(newAssignee.Email, "Task Reassigned - Study Studio", body, newAssignee);
            }

            if (oldAssignee != null)
            {
                var oldAssigneeTaskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(oldAssignee)) : "";
                await CreateInAppAsync(
                    oldAssigneeId,
                    reassignedBy,
                    "Task reassigned",
                    $"{actorName} reassigned your task: {taskTitle}",
                    AnnouncementType.TaskReassignment,
                    taskId,
                    groupId,
                    "task");

                var body = EmailTemplate.TaskReassignedEmail(taskTitle, BuildUserName(oldAssignee), newAssignee != null ? BuildUserName(newAssignee) : "Unassigned", oldAssigneeTaskUrl, GetLanguage(oldAssignee));
                await _emailService.SendEmailWithPreferenceCheckAsync(oldAssignee.Email, "Task Reassigned - Study Studio", body, oldAssignee);
            }
        }

        public async Task NotifyTaskStatusChangedAsync(Guid userId, Guid taskId, string oldStatus, string newStatus, string changedBy)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return;

            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                userId,
                userId,
                "Task status updated",
                $"{changedBy} changed status: {oldStatus} → {newStatus}",
                AnnouncementType.TaskStatusChange,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(user)) : "";
            var body = EmailTemplate.TaskStatusChangedEmail("Task", oldStatus, newStatus, changedBy, taskUrl, GetLanguage(user));
            await _emailService.SendEmailWithPreferenceCheckAsync(user.Email, "Task Status Updated - Study Studio", body, user);
        }

        public async Task NotifyTaskCompletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid completedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var actor = await _userRepository.GetByIdAsync(completedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                assigneeId,
                completedBy,
                "Task completed",
                $"{actorName} completed task: {taskTitle}",
                AnnouncementType.TaskCompleted,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(assignee)) : "";
            var body = EmailTemplate.TaskCompletedEmail(taskTitle, actorName, taskUrl, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Completed - Study Studio", body, assignee);
        }

        public async Task NotifyMentionedInCommentAsync(Guid mentionedUserId, Guid taskId, string taskTitle, Guid mentionerId, string commentPreview)
        {
            var mentionedUser = await _userRepository.GetByIdAsync(mentionedUserId);
            var mentioner = await _userRepository.GetByIdAsync(mentionerId);
            if (mentionedUser == null || mentioner == null) return;

            var mentionerName = BuildUserName(mentioner);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                mentionedUserId,
                mentionerId,
                "Mentioned in comment",
                $"{mentionerName} mentioned you in a comment: {taskTitle}",
                AnnouncementType.Mention,
                taskId,
                groupId,
                "comment");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(mentionedUser)) : "";
            var body = EmailTemplate.MentionedInCommentEmail(taskTitle, mentionerName, commentPreview, taskUrl, GetLanguage(mentionedUser));
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Task Comment - Study Studio", body, mentionedUser);
        }

        public async Task NotifyMentionedInGroupDiscussAsync(Guid mentionedUserId, Guid groupId, Guid mentionerId, string groupName, string messagePreview)
        {
            var mentionedUser = await _userRepository.GetByIdAsync(mentionedUserId);
            var mentioner = await _userRepository.GetByIdAsync(mentionerId);
            if (mentionedUser == null || mentioner == null) return;

            var mentionerName = BuildUserName(mentioner);
            await CreateInAppAsync(
                mentionedUserId,
                mentionerId,
                "Mentioned in group discussion",
                $"{mentionerName} mentioned you in {groupName}",
                AnnouncementType.Mention,
                null,
                groupId,
                "discuss");

            var body = EmailTemplate.MentionedInGroupDiscussEmail(groupName, mentionerName, messagePreview, BuildGroupDiscussUrl(groupId), GetLanguage(mentionedUser));
            await _emailService.SendEmailWithPreferenceCheckAsync(mentionedUser.Email, "Mentioned in Group Discussion - Study Studio", body, mentionedUser);
        }

        public async Task NotifyTaskDeletedAsync(Guid assigneeId, Guid taskId, string taskTitle, Guid deletedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            var actor = await _userRepository.GetByIdAsync(deletedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                assigneeId,
                deletedBy,
                "Task deleted",
                $"{actorName} deleted task: {taskTitle}",
                AnnouncementType.TaskDeleted,
                taskId,
                groupId,
                "task");

            var body = EmailTemplate.TaskDeletedEmail(taskTitle, actorName, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Deleted - Study Studio", body, assignee);
        }

        public async Task NotifyTaskUnassignedAsync(Guid previousAssigneeId, Guid taskId, string taskTitle, Guid unassignedBy)
        {
            var assignee = await _userRepository.GetByIdAsync(previousAssigneeId);
            var actor = await _userRepository.GetByIdAsync(unassignedBy);
            if (assignee == null || actor == null) return;

            var actorName = BuildUserName(actor);
            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                previousAssigneeId,
                unassignedBy,
                "Task unassigned",
                $"{actorName} removed you from task: {taskTitle}",
                AnnouncementType.TaskUnassigned,
                taskId,
                groupId,
                "task");

            var body = EmailTemplate.TaskUnassignedEmail(taskTitle, actorName, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Unassigned - Study Studio", body, assignee);
        }

        public async Task NotifyTaskOverdueAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int overdueDays)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            if (assignee == null) return;

            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                assigneeId,
                assigneeId,
                "Task overdue",
                $"Task is overdue: {taskTitle}",
                AnnouncementType.TaskOverdue,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(assignee)) : "";
            var body = EmailTemplate.TaskOverdueEmail(taskTitle, dueDate, overdueDays, taskUrl, GetLanguage(assignee));
            await _emailService.SendEmailWithPreferenceCheckAsync(assignee.Email, "Task Overdue - Study Studio", body, assignee);
        }

        public async Task NotifyTaskReminderAsync(Guid assigneeId, Guid taskId, string taskTitle, DateTime dueDate, int hoursUntilDeadline)
        {
            var assignee = await _userRepository.GetByIdAsync(assigneeId);
            if (assignee == null) return;

            var groupId = await _getGroupIdForTaskAsync(taskId);
            await CreateInAppAsync(
                assigneeId,
                assigneeId,
                "Task deadline reminder",
                $"Deadline is approaching: {taskTitle}",
                AnnouncementType.TaskReminder,
                taskId,
                groupId,
                "task");

            var taskUrl = groupId.HasValue ? BuildTaskUrl(taskId, GetLanguage(assignee)) : "";
            var body = EmailTemplate.TaskReminderEmail(taskTitle, dueDate, hoursUntilDeadline, taskUrl, GetLanguage(assignee));
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
