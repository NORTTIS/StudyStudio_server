using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using System.Threading.Tasks;

namespace StudioStudio_Server.Repositories
{
    public class PersonalTaskStatusRepository : IPersonalTaskStatusRepository
    {
        private readonly StudioDbContext _db;
        private const int MAX_RETRY = 3;
        private const long STEP = 1000;
        public PersonalTaskStatusRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(PersonalTaskStatus personalTaskStatus)
        {
            _db.PersonalTaskStatuses.Add(personalTaskStatus);
            await _db.SaveChangesAsync();
        }

        public async Task<List<PersonalTaskStatus>> GetAllByUserIdAsync(Guid userId)
        {
            return await _db.PersonalTaskStatuses
                .Where(s => s.UserId == userId)
                .AsNoTracking()
                .OrderBy(s => s.Position)
                .ToListAsync();
        }

        public async Task DeletePersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Remove(status);
            await _db.SaveChangesAsync();
        }

        public async Task UpdatePersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Update(status);
            await _db.SaveChangesAsync();
        }
        public async Task AddPersonalStatusAsync(PersonalTaskStatus status)
        {
            _db.PersonalTaskStatuses.Add(status);
            await _db.SaveChangesAsync();
        }

        public async Task SaveChangesAsync()
        {
            await _db.SaveChangesAsync();
        }

        public async Task<bool> IsNameExist(PersonalTaskStatus status)
        {
            return await _db.PersonalTaskStatuses.AnyAsync(t =>
                t.StatusName == status.StatusName &&
                t.UserId == status.UserId &&
                t.StatusId != status.StatusId);
        }

        /// <summary>
        /// Reorder status using midpoint ranking with retry and rebalance
        /// Handles concurrent updates with Serializable transaction isolation
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
                        var prev = prevStatusId.HasValue
                            ? await _db.PersonalTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == prevStatusId.Value)
                            : null;

                        var next = nextStatusId.HasValue
                            ? await _db.PersonalTaskStatuses
                                .FirstOrDefaultAsync(s => s.StatusId == nextStatusId.Value)
                            : null;

                        long newPos;

                        if (prev != null && next != null)
                        {
                            long gap = next.Position - prev.Position;

                            if (gap <= 1)
                            {
                                await RebalanceColumnInternalAsync(userId);

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
        /// Find next status after given position in the same personal
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
        /// Rebalance all statuses in a personal with proper spacing
        /// Public method with transaction
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
        /// </summary>
        private long Midpoint(long a, long b)
        {
            return (a + b) / 2;
        }

        public async Task<PersonalTaskStatus?> GetDetailAsync(Guid statusId)
        {
            return await _db.PersonalTaskStatuses
                .FirstOrDefaultAsync(t => t.StatusId == statusId);
        }
    }
}
