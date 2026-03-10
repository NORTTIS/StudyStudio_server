using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
using StudioStudio_Server.Models.Enums;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    /// <summary>
    /// Repository handling operations with Report entity
    /// </summary>
    public class ReportRepository : IReportRepository
    {
        private readonly StudioDbContext _context;

        public ReportRepository(StudioDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Add report to database
        /// Auto-set: ReportId, Status = Open, CreatedAt = UtcNow
        /// </summary>
        public async Task AddAsync(Report report)
        {
            _context.Reports.Add(report);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get reports with filtering and pagination
        /// Search by: Title and Email
        /// Filter by: Type and Status
        /// Order by: CreatedAt DESC
        /// </summary>
        public async Task<List<Report>> GetReportsAsync(
            string? searchTerm,
            ReportType? type,
            ReportStatus? status,
            int pageNumber,
            int pageSize)
        {
            var query = _context.Reports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(r =>
                    r.Title.ToLower().Contains(lowerSearchTerm) ||
                    r.Email.ToLower().Contains(lowerSearchTerm));
            }

            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            return await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .AsNoTracking()
                .ToListAsync();
        }

        /// <summary>
        /// Get total count of reports with filtering
        /// </summary>
        public async Task<int> GetTotalReportsCountAsync(
            string? searchTerm,
            ReportType? type,
            ReportStatus? status)
        {
            var query = _context.Reports.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var lowerSearchTerm = searchTerm.ToLower();
                query = query.Where(r =>
                    r.Title.ToLower().Contains(lowerSearchTerm) ||
                    r.Email.ToLower().Contains(lowerSearchTerm));
            }

            if (type.HasValue)
            {
                query = query.Where(r => r.Type == type.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            return await query.CountAsync();
        }

        /// <summary>
        /// Get count of reports by specific status
        /// </summary>
        public async Task<int> GetReportsCountByStatusAsync(ReportStatus status)
        {
            return await _context.Reports
                .Where(r => r.Status == status)
                .CountAsync();
        }

        /// <summary>
        /// Get report by ID
        /// </summary>
        public async Task<Report?> GetReportByIdAsync(Guid reportId)
        {
            return await _context.Reports
                .FirstOrDefaultAsync(r => r.ReportId == reportId);
        }

        /// <summary>
        /// Update report
        /// </summary>
        public async Task UpdateAsync(Report report)
        {
            _context.Reports.Update(report);
            await _context.SaveChangesAsync();
        }
    }
}
