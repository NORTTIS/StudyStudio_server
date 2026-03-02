using StudioStudio_Server.Data;
using StudioStudio_Server.Repositories.Interfaces;

namespace StudioStudio_Server.Repositories
{
    public class PersonalTaskStatusRepository : IPersonalTaskStatusRepository
    {
        private readonly StudioDbContext _db;
        public PersonalTaskStatusRepository(StudioDbContext db)
        {
            _db = db;
        }

        public async Task AddAsync(PersonalTaskStatus personalTaskStatus)
        {
            _db.PersonalTaskStatuses.Add(personalTaskStatus);
            await _db.SaveChangesAsync();
        }
    }
}
