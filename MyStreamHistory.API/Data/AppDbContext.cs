namespace MyStreamHistory.API.Data
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Configurations;
    using MyStreamHistory.API.Models;

    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<Stream> Streams { get; set; }
        public DbSet<TwitchCategory> TwitchCategories { get; set; }
        public DbSet<TwitchCategoryOnStream> TwitchCategoriesOnStream { get; set; }
        public DbSet<Viewer> Viewers { get; set; }
        public DbSet<ViewerPerTwitchCategoryOnStream> ViewersPerTwitchCategoryOnStream { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new CompanyConfiguration());
            modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
            modelBuilder.ApplyConfiguration(new StreamConfiguration());
            modelBuilder.ApplyConfiguration(new TwitchCategoryConfiguration());
            modelBuilder.ApplyConfiguration(new TwitchCategoryOnStreamConfiguration());
            modelBuilder.ApplyConfiguration(new ViewerConfiguration());
            modelBuilder.ApplyConfiguration(new ViewerPerTwitchCategoryOnStreamConfiguration());
        }
    }
}
