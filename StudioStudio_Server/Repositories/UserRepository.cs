using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository xử lý các thao tác CRUD với User entity
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly StudioDbContext _context;

        public UserRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Thêm user mới vào database
        /// </summary>
        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy user theo email
        /// Điều kiện: Email = {email} AND DeletedFlag = false
        /// Include: RefreshToken
        /// </summary>
        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.RefreshToken)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == email && !u.DeletedFlag);
        }

        /// <summary>
        /// Lấy user theo ID
        /// Điều kiện: UserId = {id} AND DeletedFlag = false
        /// Include: RefreshToken
        /// </summary>
        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.RefreshToken)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == id && !u.DeletedFlag);
        }

        /// <summary>
        /// Update user information
        /// </summary>
        public async Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Lấy nhiều users theo danh sách IDs
        /// Điều kiện: UserId IN {userIds} AND DeletedFlag = false
        /// Use case: Load user info cho group members, mentions, etc.
        /// </summary>
        public async Task<List<User>> GetByIdsAsync(List<Guid> userIds)
        {
            return await _context.Users
                .Where(u => userIds.Contains(u.UserId) && !u.DeletedFlag)
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
