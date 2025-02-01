namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class StreamerConfiguration : IEntityTypeConfiguration<Streamer>
    {
        public void Configure(EntityTypeBuilder<Streamer> builder)
        {
            builder.HasIndex(s => s.TwitchId).IsUnique();
        }
    }
}
