using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;

        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<User?> GetByIdAsync(Guid userId)
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _userRepository.GetByEmailAsync(email);
        }

        public async Task UpdateAsync(User user)
        {
            user.UpdatedAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
        }

        public async Task DeleteAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null)
            {
                user.Status = UserStatus.Deleted;
                user.UpdatedAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);
            }
        }
    }
}
