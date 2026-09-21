using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GreenCityReporter.Data
{
    public sealed class PostgreSqlApplicationDbContextFactory : IDesignTimeDbContextFactory<PostgreSqlApplicationDbContext>
    {
        public PostgreSqlApplicationDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set ConnectionStrings__DefaultConnection to a PostgreSQL connection string before using the PostgreSQL EF context.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new PostgreSqlApplicationDbContext(optionsBuilder.Options);
        }
    }
}