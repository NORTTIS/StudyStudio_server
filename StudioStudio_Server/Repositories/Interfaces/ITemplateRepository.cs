using StudioStudio_Server.Models.Entities;

namespace StudioStudio_Server.Repositories.Interfaces
{
    public interface ITemplateRepository
    {
        Task<Template?> GetByIdAsync(Guid templateId);
        Task<List<Template>> GetAllAsync();
        Task<List<Template>> GetByUserIdAsync(Guid userId);
        Task<Template?> GetByGroupIdAsync(Guid groupId);
        Task<List<Template>> GetSystemTemplatesAsync();
        Task<List<Template>> GetUserTemplatesAsync(Guid userId);
        Task AddAsync(Template template);
        Task UpdateAsync(Template template);
        Task DeleteAsync(Template template);
        Task<bool> ExistsAsync(Guid templateId);
    }
}
