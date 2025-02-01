namespace MyStreamHistory.API.Data
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Configurations;
    using MyStreamHistory.API.Models;

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Streamer> Streamers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new StreamerConfiguration());
        }
    }
}
