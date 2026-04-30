using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MyStreamHistory.AuthService.Domain.Entities;

namespace MyStreamHistory.AuthService.Infrastructure.Persistence.EntityConfigurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable(nameof(RefreshToken));
        
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).IsRequired();

        builder.HasIndex(x => x.TokenId).IsUnique();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.TokenFamilyId);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.ExpiresAt);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
