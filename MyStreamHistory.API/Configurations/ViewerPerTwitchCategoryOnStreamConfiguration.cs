namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class ViewerPerTwitchCategoryOnStreamConfiguration : IEntityTypeConfiguration<ViewerPerTwitchCategoryOnStream>
    {
        public void Configure(EntityTypeBuilder<ViewerPerTwitchCategoryOnStream> builder)
        {
            builder.HasOne(vpc => vpc.Viewer)
                .WithMany(v => v.ViewerPerTwitchCategoriesOnStream)
                .HasForeignKey(vpc => vpc.ViewerId);

            builder.HasOne(vpc => vpc.TwitchCategoryOnStream)
                .WithMany(tcos => tcos.Viewers)
                .HasForeignKey(vpc => vpc.TwitchCategoryOnStreamId);
        }
    }
}
