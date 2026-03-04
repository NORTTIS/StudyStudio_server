using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with GroupTaskStatus entity
    /// Note: GroupTaskStatus = Kanban columns (To Do, In Progress, Done, etc.)
    /// Uses index-based position with midpoint ranking strategy
    /// </summary>
    public class GroupTaskStatusRepository : IGroupTaskStatusRepository
    {
        private readonly StudioDbContext _context;
        private const int MAX_RETRY = 3;
        private const long STEP = 1000;

        public GroupTaskStatusRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get list of task statuses for group
        /// Condition: GroupId = {groupId}
        /// Order by: Position ASC (according to Kanban columns order)
        /// </summary>
        public async Task<List<GroupTaskStatus>> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Check if status exists
        /// Condition: StatusId = {statusId}
        /// </summary>
        public async Task<bool> ExistsAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AnyAsync(s => s.StatusId == statusId);
        }

        /// <summary>
        /// Add new task status to group
        /// </summary>
        public async Task AddAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Add(status);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Add multiple task statuses (batch insert)
        /// Use case: Initialize default statuses when creating group from template
        /// </summary>
        public async Task AddRangeAsync(List<GroupTaskStatus> statuses)
        {
            _context.GroupTaskStatuses.AddRange(statuses);
            await _context.SaveChangesAsync();
        }

        public async Task SoftDeleteAsync(GroupTaskStatus status)
        {
            status.IsDeleted = true;
            _context.GroupTaskStatuses.Update(status);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(GroupTaskStatus status)
        {
            _context.GroupTaskStatuses.Update(status);
            await _context.SaveChangesAsync();
        }

        public async Task<GroupTaskStatus?> GetDetailAsync(Guid statusId)
        {
            return await _context.GroupTaskStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StatusId == statusId && !s.IsDeleted);
        }

        public async Task<List<GroupTaskStatus>> GetByIdsAndGroupIdAsync(List<Guid> statusIds, Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(x => statusIds.Contains(x.StatusId) && x.GroupId == groupId && !x.IsDeleted)
                .ToListAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }

        public async Task<bool> NameExistsInGroupAsync(GroupTaskStatus taskStatus)
        {
            return await _context.GroupTaskStatuses.AnyAsync(t =>
                t.StatusName == taskStatus.StatusName &&
                t.GroupId == taskStatus.GroupId &&
                t.StatusId != taskStatus.StatusId &&
                !t.IsDeleted
            );
        }

        public async Task<List<GroupTaskStatus>> GetByGroupIdWithTrackingAsync(Guid groupId)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToListAsync();
        }

        /// <summary>
        /// Reorder status using midpoint ranking with retry and rebalance
        /// Handles concurrent updates with Serializable transaction isolation
        /// </summary>
        public async Task ReorderStatusAsync(Guid statusId, Guid? prevStatusId, Guid? nextStatusId, Guid groupId)
        {
            if (!prevStatusId.HasValue && !nextStatusId.HasValue)
            {
                throw new InvalidOperationException("Both prevStatusId and nextStatusId cannot be null");
            }

            for (int attempt = 1; attempt <= MAX_RETRY; attempt++)
            {
                using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        var prev = prevStatusId.HasValue
                            ? await _context.GroupTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == prevStatusId.Value && !s.IsDeleted)
                            : null;

                        var next = nextStatusId.HasValue
                            ? await _context.GroupTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == nextStatusId.Value && !s.IsDeleted)
                            : null;

                        long newPos;

                        if (prev != null && next != null)
                        {
                            long gap = next.Position - prev.Position;

                            if (gap <= 1)
                            {
                                await RebalanceColumnInternalAsync(groupId);

                                prev = prevStatusId.HasValue
                                    ? await _context.GroupTaskStatuses
                                        .FirstOrDefaultAsync(s => s.StatusId == prevStatusId.Value && !s.IsDeleted)
                                    : null;
                                next = nextStatusId.HasValue
                                    ? await _context.GroupTaskStatuses
                                        .FirstOrDefaultAsync(s => s.StatusId == nextStatusId.Value && !s.IsDeleted)
                                    : null;
                            }

                            newPos = Midpoint(prev!.Position, next!.Position);
                        }
                        else if (prev != null)
                        {
                            newPos = prev.Position + STEP;
                        }
                        else if (next != null)
                        {
                            newPos = next.Position / 2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Invalid prev/next status");
                        }

                        var status = await _context.GroupTaskStatuses
                            .FirstOrDefaultAsync(s => s.StatusId == statusId);

                        if (status == null)
                        {
                            throw new InvalidOperationException($"Status with ID {statusId} not found");
                        }

                        status.Position = (int)newPos;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return;
                    }
                    catch (DbUpdateException)
                    {
                        await transaction.RollbackAsync();
                        
                        if (attempt < MAX_RETRY)
                        {
                            await Task.Delay(50 * attempt);
                            continue;
                        }
                        throw;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }

            throw new InvalidOperationException("Failed to reorder status after maximum retries");
        }

        /// <summary>
        /// Find next status after given position in the same group
        /// </summary>
        public async Task<GroupTaskStatus?> FindNextAfterAsync(Guid groupId, long position)
        {
            return await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId && s.Position > position && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Rebalance all statuses in a group with proper spacing
        /// Public method with transaction
        /// </summary>
        public async Task RebalanceColumnAsync(Guid groupId)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    await RebalanceColumnInternalAsync(groupId);
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        /// <summary>
        /// Internal rebalance method (used within existing transactions)
        /// </summary>
        private async Task RebalanceColumnInternalAsync(Guid groupId)
        {
            var statuses = await _context.GroupTaskStatuses
                .Where(s => s.GroupId == groupId && !s.IsDeleted)
                .OrderBy(s => s.Position)
                .ToListAsync();

            long pos = STEP;
            foreach (var status in statuses)
            {
                status.Position = (int)pos;
                pos += STEP;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Calculate midpoint between two positions
        /// </summary>
        private long Midpoint(long a, long b)
        {
            return (a + b) / 2;
        }
    }
}
