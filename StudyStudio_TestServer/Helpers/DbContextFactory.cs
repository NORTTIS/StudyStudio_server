using Microsoft.EntityFrameworkCore;
using StudioStudio_Server.Data;

namespace StudioStudio_Server.Tests.Helpers
{
    public static class DbContextFactory
    {
        public static StudioDbContext Create(string dbName)
        {
            var options = new DbContextOptionsBuilder<StudioDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new StudioDbContext(options);
        }
    }
}
