using StudioStudio_Server.Data;
using StudioStudio_Server.Models.Entities;
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
        /// Auto-set: ReportId, Status = Pending, CreatedAt = UtcNow
        /// </summary>
        public async Task AddAsync(Report report)
        {
            _context.Reports.Add(report);
            await _context.SaveChangesAsync();
        }
    }
}
