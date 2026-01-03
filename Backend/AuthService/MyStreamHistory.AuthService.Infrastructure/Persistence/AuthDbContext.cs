using Microsoft.EntityFrameworkCore;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.AuthService.Infrastructure.Persistence.EntityConfigurations;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence;

public class AuthDbContext(DbContextOptions<AuthDbContext> options) : DbContext(options)
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DbSet<AuthUser> Users => Set<AuthUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuthUserConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}
