using StudioStudio_Server.Data;
using StudioStudio_Server.Exceptions;
using StudioStudio_Server.Models.DTOs.Request;
using StudioStudio_Server.Models.DTOs.Response;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;
using StudioStudio_Server.Services.Interfaces;

namespace StudioStudio_Server.Services
{
    public class TemplateService : ITemplateService
    {
        private readonly ITemplateRepository _templateRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IGroupTaskStatusRepository _groupTaskStatusRepository;
        private readonly IGroupParticipantRepository _groupParticipantRepository;
        private readonly StudioDbContext _context;

        public TemplateService(
            ITemplateRepository templateRepository,
            IGroupRepository groupRepository,
            IGroupTaskStatusRepository groupTaskStatusRepository,
            IGroupParticipantRepository groupParticipantRepository,
            StudioDbContext context)
        {
            _templateRepository = templateRepository;
            _groupRepository = groupRepository;
            _groupTaskStatusRepository = groupTaskStatusRepository;
            _groupParticipantRepository = groupParticipantRepository;
            _context = context;
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

            // Add admin as Owner of the group template
            var ownerParticipant = new GroupParticipant
            {
                ParticipantId = Guid.NewGuid(),
                GroupId = group.GroupId,
                UserId = userId,
                Role = GroupRole.Owner,
                CreatedAt = DateTime.UtcNow,
                IsApproved = true
            };
            await _groupParticipantRepository.AddAsync(ownerParticipant);

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
                UpdatedAt = DateTime.UtcNow,
                IsActive = request.IsActive
            };

            await _templateRepository.AddAsync(template);

            var statusResponses = taskStatuses.Select(s => new GroupTaskStatusResponse
            {
                StatusId = s.StatusId,
                StatusName = s.StatusName,
                Position = s.Position
            }).ToList();

            return new TemplateResponse
            {
                TemplateId = template.TemplateId,
                UserId = template.UserId,
                GroupId = template.GroupId,
                GroupName = group.GroupName,
                GroupDescription = group.Description,
                IsSystemTemplate = template.IsSystemTemplate,
                IsActive = template.IsActive,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt,
                GroupTaskStatuses = statusResponses,
                BannerUrl = group.BannerUrl,
                ColorHex = group.ColorHex
            };
        }

        public async Task<TemplateResponse> UpdateTemplateAsync(Guid userId, Guid templateId, UpdateTemplateRequest request)
        {
            var template = await _templateRepository.GetByIdIncludingInactiveAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            var group = await _groupRepository.GetByIdAdminAsync(template.GroupId);
            if (group == null)
            {
                throw new AppException(ErrorCodes.TemplateGroupNotFound, StatusCodes.Status404NotFound);
            }

            // Partial update: only apply non-null fields
            if (request.IsActive.HasValue)
            {
                template.IsActive = request.IsActive.Value;
            }

            if (!string.IsNullOrEmpty(request.GroupName))
            {
                group.GroupName = request.GroupName;
                group.UpdatedAt = DateTime.UtcNow;
                await _groupRepository.UpdateAsync(group);
            }

            if (request.GroupDescription != null)
            {
                group.Description = request.GroupDescription;
                group.UpdatedAt = DateTime.UtcNow;
                await _groupRepository.UpdateAsync(group);
            }

            // Update groupStatuses if provided (replace all: hard-delete old + insert new)
            if (request.GroupTaskStatuses != null)
            {
                var existingStatuses = await _groupTaskStatusRepository
                    .GetByGroupIdWithTrackingAsync(template.GroupId);
                await _groupTaskStatusRepository.RemoveRangeAsync(existingStatuses);

                var newStatuses = request.GroupTaskStatuses.Select(s => new GroupTaskStatus
                {
                    StatusId = Guid.NewGuid(),
                    GroupId = template.GroupId,
                    StatusName = s.StatusName,
                    Position = s.Position
                }).ToList();
                await _groupTaskStatusRepository.AddRangeAsync(newStatuses);

                template.UpdatedAt = DateTime.UtcNow;
                await _templateRepository.UpdateAsync(template);

                var statusResponses = newStatuses.Select(s => new GroupTaskStatusResponse
                {
                    StatusId = s.StatusId,
                    StatusName = s.StatusName,
                    Position = s.Position
                }).ToList();

                return new TemplateResponse
                {
                    TemplateId = template.TemplateId,
                    UserId = template.UserId,
                    GroupId = template.GroupId,
                    GroupName = group.GroupName,
                    GroupDescription = group.Description,
                    IsSystemTemplate = template.IsSystemTemplate,
                    IsActive = template.IsActive,
                    CreatedAt = template.CreatedAt,
                    UpdatedAt = template.UpdatedAt,
                    GroupTaskStatuses = statusResponses,
                    BannerUrl = group.BannerUrl,
                    ColorHex = group.ColorHex
                };
            }

            template.UpdatedAt = DateTime.UtcNow;
            await _templateRepository.UpdateAsync(template);

            // Return with existing statuses
            var currentStatuses = await _groupTaskStatusRepository.GetByGroupIdAsync(template.GroupId);
            return MapTemplate(template, group, currentStatuses);
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

            var statuses = await _groupTaskStatusRepository.GetByGroupIdAsync(template.GroupId);
            return MapTemplate(template, template.Group, statuses);
        }

        public async Task<List<TemplateResponse>> GetAllTemplatesAsync()
        {
            var templates = await _templateRepository.GetAllAsync();
            var result = new List<TemplateResponse>();
            foreach (var t in templates)
            {
                var statuses = await _groupTaskStatusRepository.GetByGroupIdAsync(t.GroupId);
                result.Add(MapTemplate(t, t.Group, statuses));
            }
            return result;
        }

        public async Task<List<TemplateResponse>> GetAllSystemTemplatesAsync()
        {
            var templates = await _templateRepository.GetAllSystemTemplatesAsync();
            var result = new List<TemplateResponse>();
            foreach (var t in templates)
            {
                var statuses = await _groupTaskStatusRepository.GetByGroupIdAsync(t.GroupId);
                result.Add(MapTemplate(t, t.Group, statuses));
            }
            return result;
        }

        public async Task<List<TemplateResponse>> GetAvailableTemplatesForUserAsync(Guid userId)
        {
            var systemTemplates = await _templateRepository.GetSystemTemplatesAsync();
            var userTemplates = await _templateRepository.GetUserTemplatesAsync(userId);

            var allTemplates = systemTemplates.Concat(userTemplates).ToList();
            var result = new List<TemplateResponse>();
            foreach (var t in allTemplates)
            {
                var statuses = await _groupTaskStatusRepository.GetByGroupIdAsync(t.GroupId);
                result.Add(MapTemplate(t, t.Group, statuses));
            }
            return result;
        }

        public async Task<TemplateResponse> GetTemplateByIdIncludingInactiveAsync(Guid templateId)
        {
            var template = await _templateRepository.GetByIdIncludingInactiveAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            var statuses = await _groupTaskStatusRepository.GetByGroupIdAsync(template.GroupId);
            return MapTemplate(template, template.Group, statuses);
        }

        public async Task HardDeleteTemplateAsync(Guid templateId)
        {
            var template = await _templateRepository.GetByIdIncludingInactiveAsync(templateId);
            if (template == null)
            {
                throw new AppException(ErrorCodes.TemplateNotFound, StatusCodes.Status404NotFound);
            }

            // Hard-delete GroupTaskStatuses
            var statuses = await _groupTaskStatusRepository.GetByGroupIdWithTrackingAsync(template.GroupId);
            await _groupTaskStatusRepository.RemoveRangeAsync(statuses);

            // Hard-delete Group
            var group = await _groupRepository.GetByIdAsync(template.GroupId);
            if (group != null)
            {
                _context.Groups.Remove(group);
                await _context.SaveChangesAsync();
            }

            // Hard-delete Template
            await _templateRepository.HardDeleteAsync(template);
        }

        // --- Private helpers ---

        private static GroupTaskStatusResponse MapStatus(GroupTaskStatus s)
            => new()
            {
                StatusId = s.StatusId,
                GroupId = s.GroupId,
                StatusName = s.StatusName,
                Position = s.Position
            };

        private static TemplateResponse MapTemplate(Template t, Group group, List<GroupTaskStatus> statuses)
            => new()
            {
                TemplateId = t.TemplateId,
                UserId = t.UserId,
                GroupId = t.GroupId,
                GroupName = group.GroupName,
                GroupDescription = group.Description,
                IsSystemTemplate = t.IsSystemTemplate,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                GroupTaskStatuses = statuses.Select(MapStatus).ToList(),
                BannerUrl = group.BannerUrl,
                ColorHex = group.ColorHex
            };
    }
}
