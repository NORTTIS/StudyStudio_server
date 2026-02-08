using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly StudioDbContext _context;
        public UserRepository(StudioDbContext context)
        {
            _context = context;
        }
        public async Task AddAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _context.Users
                .Include(u => u.RefreshToken)
                .FirstOrDefaultAsync(u => u.Email.Equals(email));
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(u => u.RefreshToken)
                .FirstOrDefaultAsync(u => u.UserId == id);
        }
    }
}
