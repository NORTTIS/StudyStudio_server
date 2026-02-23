using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class TemplateService : ITemplateService
    {
        private readonly ITemplateRepository _templateRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;

        public TemplateService(
            ITemplateRepository templateRepository,
            IGroupRepository groupRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository)
        {
            _templateRepository = templateRepository;
            _groupRepository = groupRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
        }

        public async Task<TemplateResponse> CreateTemplateAsync(Guid userId, CreateTemplateRequest request)
        {
            var group = new Group
            {
                GroupId = Guid.NewGuid(),
                GroupName = request.GroupName,
                Description = request.Description,
                CreatedBy = userId,
                StudioId = null,
                IsTemplate = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            await _groupRepository.AddAsync(group);

            var taskStatuses = request.GroupTaskStatuses.Select(s => new GroupTaskStatus
            {
                StatusId = Guid.NewGuid(),
                GroupId = group.GroupId,
                StatusName = s.StatusName,
                Position = s.Position
            }).ToList();

            await _groupTaskStatusRepository.AddRangeAsync(taskStatuses);

            var template = new Template
            {
                TemplateId = Guid.NewGuid(),
                UserId = userId,
                GroupId = group.GroupId,
                IsSystemTemplate = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _templateRepository.AddAsync(template);

            return new TemplateResponse
            {
                TemplateId = template.TemplateId,
                UserId = template.UserId,
                GroupId = template.GroupId,
                GroupName = group.GroupName,
                GroupDescription = group.Description,
                IsSystemTemplate = template.IsSystemTemplate,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        public async Task<TemplateResponse> UpdateTemplateAsync(Guid userId, Guid templateId, UpdateTemplateRequest request)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            var group = await _groupRepository.GetByIdAsync(request.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.TemplateGroupNotFound, StatusCodes.Status404NotFound);
            }

            template.GroupId = request.GroupId;
            template.IsSystemTemplate = request.IsSystemTemplate;
            template.UpdatedAt = DateTime.UtcNow;

            await _templateRepository.UpdateAsync(template);

            return new TemplateResponse
            {
                TemplateId = template.TemplateId,
                UserId = template.UserId,
                GroupId = template.GroupId,
                GroupName = group.GroupName,
                GroupDescription = group.Description,
                IsSystemTemplate = template.IsSystemTemplate,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        public async Task DeleteTemplateAsync(Guid userId, Guid templateId)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            await _templateRepository.DeleteAsync(template);

            var group = await _groupRepository.GetByIdAsync(template.GroupId);
            if (group != null)
            {
                await _groupRepository.DeleteAsync(group);
            }
        }

        public async Task<TemplateResponse> GetTemplateByIdAsync(Guid templateId)
        {
            var template = await _templateRepository.GetByIdAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            return new TemplateResponse
            {
                TemplateId = template.TemplateId,
                UserId = template.UserId,
                GroupId = template.GroupId,
                GroupName = template.Group.GroupName,
                GroupDescription = template.Group.Description,
                IsSystemTemplate = template.IsSystemTemplate,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }

        public async Task<List<TemplateResponse>> GetAllTemplatesAsync()
        {
            var templates = await _templateRepository.GetAllAsync();

            return templates.Select(t => new TemplateResponse
            {
                TemplateId = t.TemplateId,
                UserId = t.UserId,
                GroupId = t.GroupId,
                GroupName = t.Group.GroupName,
                GroupDescription = t.Group.Description,
                IsSystemTemplate = t.IsSystemTemplate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
        }

        public async Task<List<TemplateResponse>> GetAvailableTemplatesForUserAsync(Guid userId)
        {
            var systemTemplates = await _templateRepository.GetSystemTemplatesAsync();
            var userTemplates = await _templateRepository.GetUserTemplatesAsync(userId);

            var allTemplates = systemTemplates.Concat(userTemplates).ToList();

            return allTemplates.Select(t => new TemplateResponse
            {
                TemplateId = t.TemplateId,
                UserId = t.UserId,
                GroupId = t.GroupId,
                GroupName = t.Group.GroupName,
                GroupDescription = t.Group.Description,
                IsSystemTemplate = t.IsSystemTemplate,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt
            }).ToList();
        }
    }
}
