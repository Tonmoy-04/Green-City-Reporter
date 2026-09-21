using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Data
{
    public sealed class PostgreSqlApplicationDbContext : ApplicationDbContext
    {
        public PostgreSqlApplicationDbContext(DbContextOptions<PostgreSqlApplicationDbContext> options)
            : base(options)
        {
        }
    }
}