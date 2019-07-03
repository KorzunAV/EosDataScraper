using EosDataScraper.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace EosDataScraper.Api.Contexts
{
    public class PostgresDbContext : DbContext
    {
        public virtual DbSet<UserEntity> Users { get; set; }

        public PostgresDbContext()
        {
        }

        public PostgresDbContext(DbContextOptions<PostgresDbContext> options)
            : base(options)
        {
        }
    }
}