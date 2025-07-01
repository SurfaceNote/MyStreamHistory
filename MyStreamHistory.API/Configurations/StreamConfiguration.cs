namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class StreamConfiguration : IEntityTypeConfiguration<Stream>
    {
        public void Configure(EntityTypeBuilder<Stream> builder)
        {
            builder.HasIndex(vp => vp.StreamId).IsUnique();
        }
    }
}
