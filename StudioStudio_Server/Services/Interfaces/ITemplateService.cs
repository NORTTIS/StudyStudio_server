using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;

namespace StudioStudio_Server.Services.Interfaces
{
    public interface ITemplateService
    {
        Task<TemplateResponse> CreateTemplateAsync(Guid userId, CreateTemplateRequest request);
        Task<TemplateResponse> UpdateTemplateAsync(Guid userId, Guid templateId, UpdateTemplateRequest request);
        Task DeleteTemplateAsync(Guid userId, Guid templateId);
        Task<TemplateResponse> GetTemplateByIdAsync(Guid templateId);
        Task<TemplateResponse> GetTemplateByIdIncludingInactiveAsync(Guid templateId);
        Task HardDeleteTemplateAsync(Guid templateId);
        Task<List<TemplateResponse>> GetAllSystemTemplatesAsync();
        Task<List<TemplateResponse>> GetAvailableTemplatesForUserAsync(Guid userId);
    }
}
