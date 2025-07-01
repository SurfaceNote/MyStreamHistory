namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class TwitchCategoryConfiguration : IEntityTypeConfiguration<TwitchCategory>
    {
        public void Configure(EntityTypeBuilder<TwitchCategory> builder)
        {
            builder.HasIndex(tc => tc.TwitchId).IsUnique();
        }
    }
}
