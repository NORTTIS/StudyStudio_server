using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using System.Threading.Tasks;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with PersonalTaskStatus entity
    /// Manages personal Kanban columns for individual users
    /// Uses index-based position with midpoint ranking strategy
    /// </summary>
    public class PersonalTaskStatusRepository(StudioDbContext db) : IPersonalTaskStatusRepository
    {
        private readonly StudioDbContext _db = db;
        private const int MAX_RETRY = 3;
        private const long STEP = 1000;

        /// <summary>
        /// Add new personal task status to database
        /// </summary>
        public async Task AddAsync(PersonalTaskStatus personalTaskStatus)
        {
            _db.PersonalTaskStatuses.Add(personalTaskStatus);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Get all personal task statuses for a user
        /// Condition: UserId = {userId}
        /// Order by: Position ASC (according to Kanban columns order)
        /// </summary>
        public async Task<List<PersonalTaskStatus>> GetAllByUserIdAsync(Guid userId)
        {
            return await _db.PersonalTaskStatuses
                .Where(s => s.UserId == userId)
                .AsNoTracking()
                .OrderBy(s => s.Position)
                .ToListAsync();
        }

        /// <summary>
        /// Delete personal task status from database (hard delete)
        /// </summary>
        public async Task DeletePersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Remove(status);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Update personal task status information
        /// </summary>
        public async Task UpdatePersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Update(status);
            await _db.SaveChangesAsync();
        }
        
        /// <summary>
        /// Add personal task status to database (alias of AddAsync)
        /// </summary>
        public async Task AddPersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Add(status);
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Save changes to database
        /// </summary>
        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Check if status name already exists for user
        /// Condition: StatusName = {name} AND UserId = {userId} AND StatusId != {currentStatusId}
        /// Use case: Validate status name uniqueness per user
        /// </summary>
        public async Task<bool> IsNameExist(PersonalTaskStatus status)
        {
            return await _db.PersonalTaskStatuses.AnyAsync(t =>
                t.StatusName == status.StatusName &&
                t.UserId == status.UserId &&
                t.StatusId != status.StatusId);
        }

        /// <summary>
        /// Reorder personal status using midpoint ranking with retry and rebalance
        /// Handles concurrent updates with Serializable transaction isolation
        /// Supports: dragging status to different position
        /// </summary>
        public async Task ReorderStatusAsync(Guid statusId, Guid? prevStatusId, Guid? nextStatusId, Guid userId)
        {
            if (!prevStatusId.HasValue && !nextStatusId.HasValue)
            {
                throw new InvalidOperationException("Both prevStatusId and nextStatusId cannot be null");
            }

            for (int attempt = 1; attempt <= MAX_RETRY; attempt++)
            {
                using (var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // Load previous and next statuses
                        var prev = prevStatusId.HasValue
                            ? await _db.PersonalTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == prevStatusId.Value)
                            : null;

                        var next = nextStatusId.HasValue
                            ? await _db.PersonalTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == nextStatusId.Value)
                            : null;

                        long newPos;

                        // Calculate new position based on neighbors
                        if (prev != null && next != null)
                        {
                            long gap = next.Position - prev.Position;

                            // If gap is too small, rebalance first
                            if (gap <= 1)
                            {
                                await RebalanceColumnInternalAsync(userId);

                                // Reload after rebalance
                                prev = prevStatusId.HasValue
                                    ? await _db.PersonalTaskStatuses
                                        .FirstOrDefaultAsync(s => s.StatusId == prevStatusId.Value)
                                    : null;
                                next = nextStatusId.HasValue
                                    ? await _db.PersonalTaskStatuses
                                        .FirstOrDefaultAsync(s => s.StatusId == nextStatusId.Value)
                                    : null;
                            }

                            newPos = Midpoint(prev!.Position, next!.Position);
                        }
                        else if (prev != null)
                        {
                            // Place after prev (at the end)
                            newPos = prev.Position + STEP;
                        }
                        else if (next != null)
                        {
                            // Place before next (at the beginning)
                            newPos = next.Position / 2;
                        }
                        else
                        {
                            throw new InvalidOperationException("Invalid prev/next status");
                        }

                        // Update status position
                        var status = await _db.PersonalTaskStatuses
                            .FirstOrDefaultAsync(s => s.StatusId == statusId);

                        if (status == null)
                        {
                            throw new InvalidOperationException($"Status with ID {statusId} not found");
                        }

                        status.Position = (int)newPos;
                        await _db.SaveChangesAsync();
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
        /// Find next status after given position for user
        /// Condition: UserId = {userId} AND Position > {position}
        /// Order by: Position ASC (get the immediate next)
        /// </summary>
        public async Task<PersonalTaskStatus?> FindNextAfterAsync(Guid userId, long position)
        {
            return await _db.PersonalTaskStatuses
                .Where(s => s.UserId == userId && s.Position > position)
                .OrderBy(s => s.Position)
                .AsNoTracking()
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Rebalance all statuses for a user with proper spacing
        /// Public method with transaction
        /// Use case: When positions are too close together
        /// </summary>
        public async Task RebalanceColumnAsync(Guid userId)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable))
            {
                try
                {
                    await RebalanceColumnInternalAsync(userId);
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
        /// Redistributes position values with STEP spacing (1000, 2000, 3000, ...)
        /// </summary>
        private async Task RebalanceColumnInternalAsync(Guid userId)
        {
            var statuses = await _db.PersonalTaskStatuses
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.Position)
                .ToListAsync();

            long pos = STEP;
            foreach (var status in statuses)
            {
                status.Position = (int)pos;
                pos += STEP;
            }

            await _db.SaveChangesAsync();
        }

        /// <summary>
        /// Calculate midpoint between two positions
        /// Used for inserting status between two existing statuses
        /// </summary>
        private long Midpoint(long a, long b)
        {
            return (a + b) / 2;
        }

        /// <summary>
        /// Get personal task status by ID
        /// Condition: StatusId = {statusId}
        /// </summary>
        public async Task<PersonalTaskStatus?> GetDetailAsync(Guid statusId)
        {
            return await _db.PersonalTaskStatuses
                .FirstOrDefaultAsync(t => t.StatusId == statusId);
        }
    }
}
