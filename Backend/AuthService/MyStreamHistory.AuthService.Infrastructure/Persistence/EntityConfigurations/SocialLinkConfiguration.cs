using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.EntityConfigurations;

public class SocialLinkConfiguration : IEntityTypeConfiguration<SocialLink>
{
    public void Configure(EntityTypeBuilder<SocialLink> builder)
    {
        builder.ToTable(nameof(SocialLink));
        
        builder.HasKey(sl => sl.Id);
        
        builder.Property(sl => sl.UserId)
            .IsRequired();
        
        builder.Property(sl => sl.SocialNetworkType)
            .IsRequired()
            .HasConversion<string>();
        
        builder.Property(sl => sl.Path)
            .IsRequired()
            .HasMaxLength(500);
        
        builder.Property(sl => sl.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");
        
        builder.Property(sl => sl.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()");

        // Связь с AuthUser
        builder.HasOne(sl => sl.User)
            .WithMany()
            .HasForeignKey(sl => sl.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Уникальный индекс на комбинацию UserId и SocialNetworkType
        builder.HasIndex(sl => new { sl.UserId, sl.SocialNetworkType })
            .IsUnique();
    }
}

