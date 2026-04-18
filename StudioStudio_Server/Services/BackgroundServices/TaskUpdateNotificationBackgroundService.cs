using StudioStudio_Server.Models.BackgroundJobs;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services.BackgroundServices
{
    public class TaskUpdateNotificationBackgroundService(
        ITaskUpdateNotificationQueue queue,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<TaskUpdateNotificationBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Task update notification worker started");

            while (!stoppingToken.IsCancellationRequested)
            {
                TaskUpdateNotificationLease? lease = null;
                try
                {
                    var dequeuedLease = await queue.DequeueAsync(stoppingToken);
                    if (dequeuedLease is null)
                    {
                        continue;
                    }

                    lease = dequeuedLease;
                    var job = lease.Job;

                    using var scope = serviceScopeFactory.CreateScope();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                    var participantRepository = scope.ServiceProvider.GetRequiredService<IGroupParticipantRepository>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    var currentUser = await userRepository.GetByIdAsync(job.ActorUserId);
                    if (currentUser == null)
                    {
                        continue;
                    }

                    var userIds = new HashSet<Guid> { job.ActorUserId };

                    if (job.OldAssigneeId.HasValue)
                    {
                        userIds.Add(job.OldAssigneeId.Value);
                    }

                    if (job.HasAssigneeUpdate && job.RequestedAssigneeId.HasValue)
                    {
                        userIds.Add(job.RequestedAssigneeId.Value);
                    }

                    List<GroupParticipant> participants = [];
                    if (job.GroupId.HasValue && job.ReachedCompletion)
                    {
                        participants = await participantRepository.GetAllByGroupIdAsync(job.GroupId.Value);
                        foreach (var participant in participants)
                        {
                            if (participant.Role is GroupRole.Owner or GroupRole.Moderator)
                            {
                                userIds.Add(participant.UserId);
                            }
                        }
                    }

                    var users = await userRepository.GetByIdsAsync(userIds.ToList());
                    users = users.Where(u => u.Status != UserStatus.Deleted).ToList();
                    var userDict = users.ToDictionary(u => u.UserId);

                    var notificationTasks = new List<Task>();

                    if (job.ReachedCompletion && job.OldAssigneeId.HasValue)
                    {
                        var completionRecipients = new HashSet<Guid>();
                        completionRecipients.Add(job.OldAssigneeId.Value);

                        foreach (var participant in participants)
                        {
                            if (participant.Role is GroupRole.Owner or GroupRole.Moderator)
                            {
                                completionRecipients.Add(participant.UserId);
                            }
                        }

                        foreach (var recipientId in completionRecipients)
                        {
                            if (recipientId == job.ActorUserId) continue;
                            if (!userDict.TryGetValue(recipientId, out var recipient)) continue;

                            notificationTasks.Add(notificationService.NotifyTaskCompletedAsync(recipient, currentUser, job.TaskId, job.TaskTitle, stoppingToken));
                        }
                    }

                    if (job.HasAssigneeUpdate)
                    {
                        if (job.RequestedAssigneeId == null)
                        {
                            User? oldAssignee = null;
                            if (job.OldAssigneeId.HasValue)
                            {
                                userDict.TryGetValue(job.OldAssigneeId.Value, out oldAssignee);
                            }

                            if (oldAssignee != null)
                            {
                                if (oldAssignee.UserId != job.ActorUserId)
                                {
                                    notificationTasks.Add(notificationService.NotifyTaskUnassignedAsync(oldAssignee, currentUser, job.TaskId, job.TaskTitle, stoppingToken));
                                }
                            }
                        }
                        else
                        {
                            if (!userDict.TryGetValue(job.RequestedAssigneeId.Value, out var newAssignee))
                            {
                                newAssignee = null;
                            }

                            if (newAssignee != null)
                            {
                                User? oldAssignee = null;
                                if (job.OldAssigneeId.HasValue)
                                {
                                    userDict.TryGetValue(job.OldAssigneeId.Value, out oldAssignee);
                                }

                                if (job.OldAssigneeId.HasValue && job.OldAssigneeId.Value != job.RequestedAssigneeId.Value && oldAssignee != null)
                                {
                                    notificationTasks.Add(notificationService.NotifyTaskReassignedAsync(newAssignee, oldAssignee, currentUser, job.TaskId, job.TaskTitle, stoppingToken));
                                }
                                else if (!job.OldAssigneeId.HasValue)
                                {
                                    if (newAssignee.UserId != job.ActorUserId)
                                    {
                                        notificationTasks.Add(notificationService.NotifyTaskAssignedAsync(newAssignee, currentUser, job.TaskId, job.TaskTitle, job.DueDate, stoppingToken));
                                    }
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(job.OldStatusName)
                        && !string.IsNullOrWhiteSpace(job.NewStatusName)
                        && !string.Equals(job.OldStatusName, job.NewStatusName, StringComparison.Ordinal)
                        && job.OldAssigneeId.HasValue
                        && userDict.TryGetValue(job.OldAssigneeId.Value, out var statusAssignee))
                    {
                        var changedBy = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                        notificationTasks.Add(notificationService.NotifyTaskStatusChangedAsync(statusAssignee, currentUser, job.TaskId, job.OldStatusName, job.NewStatusName, changedBy, stoppingToken));
                    }

                    if (notificationTasks.Count > 0)
                    {
                        await Task.WhenAll(notificationTasks);
                    }

                    await queue.AcknowledgeAsync(lease, stoppingToken);
                    lease = null;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while processing task update notification job");
                    if (lease != null)
                    {
                        await queue.AbandonAsync(lease, stoppingToken);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }

            logger.LogInformation("Task update notification worker stopped");
        }
    }
}