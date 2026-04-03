using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with Studio entity
    /// </summary>
    public class StudioRepository : IStudioRepository
    {
        private readonly StudioDbContext _context;

        public StudioRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get studio by ID
        /// Condition: StudioId = {studioId} AND IsDeleted = false
        /// </summary>
        public async Task<Studio?> GetByIdAsync(Guid studioId)
        {
            return await _context.Studios
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudioId == studioId && !s.IsDeleted);
        }

        /// <summary>
        /// Get multiple studios by list of IDs
        /// Condition: StudioId IN {studioIds} AND IsDeleted = false
        /// Return: Empty list if studioIds is empty
        /// </summary>
        public async Task<List<Studio>> GetByIdsAsync(List<Guid> studioIds)
        {
            if (studioIds.Count == 0)
            {
                return new List<Studio>();
            }

            return await _context.Studios
                .Where(s => studioIds.Contains(s.StudioId) && !s.IsDeleted)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get list of studios owned by user
        /// Condition: OwnerId = {ownerId}
        /// Order by: StudioName DESC, CreatedAt DESC
        /// </summary>
        public async Task<List<Studio>> GetByOwnerIdAsync(Guid ownerId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == ownerId && !s.IsDeleted)
                .OrderByDescending(s => s.StudioName)
                .ThenByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Count studios created by user
        /// Condition: OwnerId = {userId}
        /// Use case: Check studio creation limit
        /// </summary>
        public async Task<int> CountStudioCreatedByUserAsync(Guid userId)
        {
            return await _context.Studios
                .Where(s => s.OwnerId == userId && !s.IsDeleted)
                .CountAsync();
        }

        /// <summary>
        /// Check if user is owner of studio
        /// Condition: StudioId = {studioId} AND OwnerId = {userId} AND IsDeleted = false
        /// </summary>
        public async Task<bool> IsUserStudioOwnerAsync(Guid studioId, Guid userId)
        {
            return await _context.Studios
                .AnyAsync(s => s.StudioId == studioId && s.OwnerId == userId && !s.IsDeleted);
        }

        /// <summary>
        /// Add new studio to database
        /// </summary>
        public async Task CreateStudioAsync(Studio studio)
        {
            _context.Studios.Add(studio);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update studio information
        /// </summary>
        public async Task UpdateStudioAsync(Studio studio)
        {
            _context.Studios.Update(studio);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get studio by ID with tracking (for updates)
        /// </summary>
        public async Task<Studio?> GetByIdForUpdateAsync(Guid studioId)
        {
            return await _context.Studios
                .FirstOrDefaultAsync(s => s.StudioId == studioId && !s.IsDeleted);
        }

        /// <summary>
        /// Soft delete studio
        /// </summary>
        public async Task DeleteStudioAsync(Studio studio)
        {
            studio.IsDeleted = true;
            studio.UpdatedAt = DateTime.UtcNow;
            _context.Studios.Update(studio);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get groups belonging to a studio
        /// Condition: StudioId = {studioId} AND IsActive = true
        /// </summary>
        public async Task<List<Group>> GetGroupsByStudioIdAsync(Guid studioId)
        {
            return await _context.Groups
                .Where(g => g.StudioId == studioId && g.IsActive)
                .Include(g => g.Participants)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<bool> IsStudioNameExistByOwnerIdAsync(string studioName, Guid ownerId)
        {
            var trimmedName = studioName.Trim();
            return await _context.Studios
                .AnyAsync(s => s.StudioName.Trim() == trimmedName && s.OwnerId == ownerId && !s.IsDeleted);
        }

        /// <summary>
        /// Check if studio name already exists for owner, excluding a specific studio (for update)
        /// </summary>
        public async Task<bool> IsStudioNameExistExcludingStudioAsync(string studioName, Guid ownerId, Guid excludeStudioId)
        {
            var trimmedName = studioName.Trim();
            return await _context.Studios
                .AnyAsync(s => s.StudioName.Trim() == trimmedName && s.OwnerId == ownerId && !s.IsDeleted && s.StudioId != excludeStudioId);
        }
        
        /// <summary>
        /// Get group by ID (including deleted studio for admin)
        /// </summary>
        public async Task<Studio?> GetByIdAdminAsync(Guid studioId)
        {
            return await _context.Studios
                .FirstOrDefaultAsync(g => g.StudioId == studioId);
        }

        /// <summary>
        /// Get paginated studios with search filter for admin
        /// </summary>
        public async Task<(List<Studio> Studios, int TotalCount)> GetStudiosAsync(
            string? searchTerm,
            int pageNumber,
            int pageSize)
        {
            var query = _context.Studios.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(s => s.StudioName.Contains(searchTerm));
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            // Apply pagination and ordering
            var studios = await query
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();

            return (studios, totalCount);
        }

        /// <summary>
        /// Get summary statistics for studios (raw values)
        /// </summary>
        public async Task<(int TotalStudios, int ActiveStudios, int InactiveStudios, int TotalMembers, int TotalGroups)> GetStudioSummaryAsync()
        {
            var studios = _context.Studios.Where(s => !s.IsDeleted);
            var studioIdList = studios.Select(s => s.StudioId).ToList();

            var totalMembers = await _context.StudioParticipants
                .Where(sp => studioIdList.Contains(sp.StudioId) && sp.IsApproved)
                .CountAsync();

            var totalGroups = await _context.Groups
                .Where(g => g.StudioId.HasValue && studioIdList.Contains(g.StudioId.Value) && g.IsActive)
                .CountAsync();

            var total = await studios.CountAsync();
            var active = await studios.CountAsync(s => !s.IsDeleted);
            var inactive = await studios.CountAsync(s => s.IsDeleted);

            return (total, active, inactive, totalMembers, totalGroups);
        }

        /// <summary>
        /// Get member counts for a list of studios (approved members only)
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetMemberCountsAsync(List<Guid> studioIds)
        {
            if (studioIds == null || studioIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var counts = await _context.StudioParticipants
                .Where(sp => studioIds.Contains(sp.StudioId) && sp.IsApproved)
                .GroupBy(sp => sp.StudioId)
                .Select(g => new { StudioId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StudioId, x => x.Count);

            return studioIds.ToDictionary(s => s, s => counts.ContainsKey(s) ? counts[s] : 0);
        }

        /// <summary>
        /// Get group counts for a list of studios
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetGroupCountsAsync(List<Guid> studioIds)
        {
            if (studioIds == null || studioIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            var counts = await _context.Groups
                .Where(g => g.StudioId.HasValue && studioIds.Contains(g.StudioId.Value) && g.IsActive)
                .GroupBy(g => g.StudioId!.Value)
                .Select(g => new { StudioId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.StudioId, x => x.Count);

            return studioIds.ToDictionary(s => s, s => counts.ContainsKey(s) ? counts[s] : 0);
        }

        /// <summary>
        /// Get task counts for a list of studios (via groups in studio)
        /// </summary>
        public async Task<Dictionary<Guid, int>> GetTaskCountsAsync(List<Guid> studioIds)
        {
            if (studioIds == null || studioIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            // Get group IDs for all studios
            var groupIdsByStudio = await _context.Groups
                .Where(g => g.StudioId.HasValue && studioIds.Contains(g.StudioId.Value) && g.IsActive)
                .Select(g => new { g.StudioId, g.GroupId })
                .ToListAsync();

            var groupIds = groupIdsByStudio.Select(x => x.GroupId).ToList();

            var taskCounts = await _context.Tasks
                .Where(t => groupIds.Contains(t.GroupId!.Value))
                .GroupBy(t => t.GroupId)
                .Select(g => new { GroupId = g.Key!.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.GroupId, x => x.Count);

            // Map group task counts back to studio level
            var result = new Dictionary<Guid, int>();
            foreach (var studioId in studioIds)
            {
                var studioGroupIds = groupIdsByStudio.Where(x => x.StudioId == studioId).Select(x => x.GroupId).ToList();
                result[studioId] = studioGroupIds.Sum(gid => taskCounts.GetValueOrDefault(gid));
            }

            return result;
        }

        /// <summary>
        /// Get last activity for a list of studios
        /// Last activity = MAX(Studio.UpdatedAt, MAX(Group.UpdatedAt), MAX(Task.UpdatedAt), MAX(GroupMessage.CreatedAt))
        /// </summary>
        public async Task<Dictionary<Guid, DateTime?>> GetLastActivityAsync(List<Guid> studioIds)
        {
            if (studioIds == null || studioIds.Count == 0)
            {
                return new Dictionary<Guid, DateTime?>();
            }

            // Get studio UpdatedAt
            var studioUpdatedAt = await _context.Studios
                .Where(s => studioIds.Contains(s.StudioId))
                .Select(s => new { s.StudioId, s.UpdatedAt })
                .ToDictionaryAsync(x => x.StudioId, x => (DateTime?)x.UpdatedAt);

            // Get group IDs per studio
            var groupIdsByStudio = await _context.Groups
                .Where(g => g.StudioId.HasValue && studioIds.Contains(g.StudioId.Value) && g.IsActive)
                .Select(g => new { g.StudioId, g.GroupId })
                .ToListAsync();

            var groupIds = groupIdsByStudio.Select(x => x.GroupId).ToList();

            // Get max group UpdatedAt per studio
            var groupUpdatedAt = await _context.Groups
                .Where(g => g.StudioId.HasValue && studioIds.Contains(g.StudioId.Value) && g.IsActive)
                .GroupBy(g => g.StudioId!.Value)
                .Select(g => new { StudioId = g.Key, LastUpdated = g.Max(x => x.UpdatedAt) })
                .ToDictionaryAsync(x => x.StudioId, x => (DateTime?)x.LastUpdated);

            // Get max task UpdatedAt per group, then aggregate to studio
            var taskUpdatedAtByGroup = await _context.Tasks
                .Where(t => groupIds.Contains(t.GroupId!.Value))
                .GroupBy(t => t.GroupId)
                .Select(g => new { GroupId = g.Key!.Value, LastUpdated = g.Max(t => t.UpdatedAt) })
                .ToDictionaryAsync(x => x.GroupId, x => (DateTime?)x.LastUpdated);

            var taskUpdatedAtByStudio = new Dictionary<Guid, DateTime?>();
            foreach (var studioId in studioIds)
            {
                var studioGroupIds = groupIdsByStudio.Where(x => x.StudioId == studioId).Select(x => x.GroupId).ToList();
                var maxTaskTime = studioGroupIds.Max(gid => taskUpdatedAtByGroup.GetValueOrDefault(gid));
                taskUpdatedAtByStudio[studioId] = maxTaskTime;
            }

            // Get max message CreatedAt per group, then aggregate to studio
            var messageCreatedAtByGroup = await _context.GroupMessages
                .Where(m => groupIds.Contains(m.GroupId))
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, LastMessage = g.Max(m => m.CreatedAt) })
                .ToDictionaryAsync(x => x.GroupId, x => (DateTime?)x.LastMessage);

            var messageCreatedAtByStudio = new Dictionary<Guid, DateTime?>();
            foreach (var studioId in studioIds)
            {
                var studioGroupIds = groupIdsByStudio.Where(x => x.StudioId == studioId).Select(x => x.GroupId).ToList();
                var maxMessageTime = studioGroupIds.Max(gid => messageCreatedAtByGroup.GetValueOrDefault(gid));
                messageCreatedAtByStudio[studioId] = maxMessageTime;
            }

            // Calculate max for each studio
            var result = new Dictionary<Guid, DateTime?>();
            foreach (var studioId in studioIds)
            {
                var studioTime = studioUpdatedAt.GetValueOrDefault(studioId);
                var groupTime = groupUpdatedAt.GetValueOrDefault(studioId);
                var taskTime = taskUpdatedAtByStudio.GetValueOrDefault(studioId);
                var messageTime = messageCreatedAtByStudio.GetValueOrDefault(studioId);

                var maxTime = studioTime;
                if (groupTime.HasValue && (!maxTime.HasValue || groupTime > maxTime))
                    maxTime = groupTime;
                if (taskTime.HasValue && (!maxTime.HasValue || taskTime > maxTime))
                    maxTime = taskTime;
                if (messageTime.HasValue && (!maxTime.HasValue || messageTime > maxTime))
                    maxTime = messageTime;

                result[studioId] = maxTime;
            }

            return result;
        }

        /// <summary>
        /// Get owner info (name + email) for a list of owner IDs
        /// </summary>
        public async Task<Dictionary<Guid, (string Name, string Email)>> GetOwnerInfosAsync(List<Guid> ownerIds)
        {
            var validIds = ownerIds.Distinct().ToList();
            if (validIds.Count == 0)
            {
                return new Dictionary<Guid, (string Name, string Email)>();
            }

            var users = await _context.Users
                .Where(u => validIds.Contains(u.UserId))
                .Select(u => new { u.UserId, FullName = u.FirstName + " " + u.LastName, u.Email })
                .ToDictionaryAsync(
                    x => x.UserId,
                    x => (x.FullName, x.Email ?? ""));

            return users;
        }

        /// <summary>
        /// Kiểm tra xem đã có studio active nào của cùng owner có tên trùng không
        /// Chỉ kiểm tra studio đang active (IsDeleted = false)
        /// </summary>
        public async Task<bool> HasActiveStudioWithNameAsync(Guid ownerId, string studioName, Guid excludeStudioId)
        {
            var trimmedName = studioName.Trim();

            return await _context.Studios
                .AnyAsync(s =>
                    s.StudioId != excludeStudioId &&
                    s.OwnerId == ownerId &&
                    s.StudioName.Trim() == trimmedName &&
                    !s.IsDeleted);
        }
    }
}
