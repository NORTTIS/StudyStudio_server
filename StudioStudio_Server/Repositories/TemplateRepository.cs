using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling CRUD operations with Template entity
    /// </summary>
    public class TemplateRepository : ITemplateRepository
    {
        private readonly StudioDbContext _context;

        public TemplateRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get template by ID
        /// Condition: TemplateId = {templateId} AND IsActive = true
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
        /// Get template by GroupId
        /// Condition: GroupId = {groupId} AND IsActive = true
        /// Include: Group, User
        /// Use case: Check which group is using this template
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
        /// Get all templates
        /// Condition: IsActive = true
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
        /// Get templates created by user
        /// Condition: UserId = {userId} AND IsActive = true
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
        /// Get system templates (built-in templates)
        /// Condition: IsSystemTemplate = true AND IsActive = true
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
        /// Get user-created templates (not system templates)
        /// Condition: UserId = {userId} AND IsSystemTemplate = false AND IsActive = true
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
        /// Check if template exists
        /// Condition: TemplateId = {templateId} AND IsActive = true
        /// </summary>
        public async Task<bool> ExistsAsync(Guid templateId)
        {
            return await _context.Templates
                .AnyAsync(t => t.TemplateId == templateId && t.IsActive);
        }

        /// <summary>
        /// Add new template to database
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
