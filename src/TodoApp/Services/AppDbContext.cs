using Microsoft.EntityFrameworkCore;

namespace TodoApp.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<DbUser> Users { get; protected set; } = null!;
        public DbSet<DbSession> Sessions { get; protected set; } = null!;

        public AppDbContext(DbContextOptions options) : base(options) { }
    }
}
