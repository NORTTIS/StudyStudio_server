using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository x? l? các thao tác CRUD v?i Template entity
    /// </summary>
    public class TemplateRepository : ITemplateRepository
    {
        private readonly StudioDbContext _context;

        public TemplateRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// L?y template theo ID
        /// Ði?u ki?n: TemplateId = {templateId} AND IsActive = true
        /// Include: Group, User
        /// </summary>
        public async Task<Template?> GetByIdAsync(Guid templateId)
        {
            return await _context.Templates
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.TemplateId == templateId && t.IsActive);
        }

        /// <summary>
        /// L?y template theo GroupId
        /// Ði?u ki?n: GroupId = {groupId} AND IsActive = true
        /// Include: Group, User
        /// Use case: Check template ðang ðý?c s? d?ng b?i group nào
        /// </summary>
        public async Task<Template?> GetByGroupIdAsync(Guid groupId)
        {
            return await _context.Templates
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.GroupId == groupId && t.IsActive);
        }

        /// <summary>
        /// L?y t?t c? templates
        /// Ði?u ki?n: IsActive = true
        /// Include: Group, User
        /// </summary>
        public async Task<List<Template>> GetAllAsync()
        {
            return await _context.Templates
                .Where(t => t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// L?y templates do user t?o
        /// Ði?u ki?n: UserId = {userId} AND IsActive = true
        /// Include: Group, User
        /// </summary>
        public async Task<List<Template>> GetByUserIdAsync(Guid userId)
        {
            return await _context.Templates
                .Where(t => t.UserId == userId && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// L?y system templates (built-in templates)
        /// Ði?u ki?n: IsSystemTemplate = true AND IsActive = true
        /// Include: Group, User
        /// </summary>
        public async Task<List<Template>> GetSystemTemplatesAsync()
        {
            return await _context.Templates
                .Where(t => t.IsSystemTemplate && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// L?y user-created templates (không ph?i system templates)
        /// Ði?u ki?n: UserId = {userId} AND IsSystemTemplate = false AND IsActive = true
        /// Include: Group, User
        /// </summary>
        public async Task<List<Template>> GetUserTemplatesAsync(Guid userId)
        {
            return await _context.Templates
                .Where(t => t.UserId == userId && !t.IsSystemTemplate && t.IsActive)
                .Include(t => t.Group)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Ki?m tra template có t?n t?i không
        /// Ði?u ki?n: TemplateId = {templateId} AND IsActive = true
        /// </summary>
        public async Task<bool> ExistsAsync(Guid templateId)
        {
            return await _context.Templates
                .AnyAsync(t => t.TemplateId == templateId && t.IsActive);
        }

        /// <summary>
        /// Thêm template m?i vào database
        /// </summary>
        public async Task AddAsync(Template template)
        {
            _context.Templates.Add(template);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Update template information
        /// </summary>
        public async Task UpdateAsync(Template template)
        {
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Soft delete template
        /// Set IsActive = false, UpdatedAt = UtcNow
        /// </summary>
        public async Task DeleteAsync(Template template)
        {
            template.IsActive = false;
            template.UpdatedAt = DateTime.UtcNow;
            _context.Templates.Update(template);
            await _context.SaveChangesAsync();
        }
    }
}
