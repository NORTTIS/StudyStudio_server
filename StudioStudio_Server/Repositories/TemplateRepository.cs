using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class TemplateRepository : ITemplateRepository
    {
        private readonly StudioDbContext _context;

        public TemplateRepository(StudioDbContext context)
        {
            _context = context;
        }

        public async Task<Template?> GetByIdAsync(Guid templateId)
        {
            return await _context.Templates
                .Include(t => t.Group)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TemplateId == templateId && t.IsActive);
        }

        public async Task<List<Template>> GetAllAsync()
        {
            return await _context.Templates
                .Where(t => t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Template>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Templates
                .Where(t => t.UserId == userId && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<Template?> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.Templates
                .Include(t => t.Group)
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.GroupId == groupId && t.IsActive);
        }

        public async Task<List<Template>> GetSystemTemplatesAsync()
        {
            return await _context.Templates
                .Where(t => t.IsSystemTemplate && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Template>> GetUserTemplatesAsync(Guid userId)
        {
            return await _context.Templates
                .Where(t => t.UserId == userId && !t.IsSystemTemplate && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task AddAsync(Template template)
        {
            _context.Templates.Add(template);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Template template)
        {
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Template template)
        {
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();
        }

        public async Task<bool> ExistsAsync(Guid templateId)
        {
            return await _context.Templates
                .AnyAsync(t => t.TemplateId == templateId && t.IsActive);
        }
    }
}
