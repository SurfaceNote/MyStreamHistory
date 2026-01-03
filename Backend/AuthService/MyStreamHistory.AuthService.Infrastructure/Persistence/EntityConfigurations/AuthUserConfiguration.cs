using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.EntityConfigurations;

public class AuthUserConfiguration : IEntityTypeConfiguration<AuthUser>
{
    public void Configure(EntityTypeBuilder<AuthUser> builder)
    {
        builder.ToTable(nameof(AuthUser));
        
        builder.HasKey(u => u.Id);
        
        builder.Property(u => u.TwitchId)
            .IsRequired();
        
        builder.Property(u => u.Email)
            .IsRequired(false);
        
        builder.Property(u => u.Login)
            .IsRequired()
            .HasMaxLength(50);
        
        builder.Property(u => u.DisplayName)
            .IsRequired().
            HasMaxLength(50);
        
        builder.Property(u => u.TwitchAccessToken)
            .IsRequired(false);
        
        builder.Property(u => u.TwitchRefreshToken)
            .IsRequired(false);
        
        builder.Property(u => u.IsTwitchTokenFresh)
            .IsRequired();
        
        builder.Property(u => u.TwitchCreatedAt)
            .IsRequired();
        
        builder.Property(u => u.SiteCreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");
        
        builder.Property(u => u.LastActivityAt)
            .IsRequired()
            .HasDefaultValueSql("now()");
        
        builder.Property(u => u.LastLoginAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        builder.HasIndex(u => u.TwitchId)
            .IsUnique();
    }
}