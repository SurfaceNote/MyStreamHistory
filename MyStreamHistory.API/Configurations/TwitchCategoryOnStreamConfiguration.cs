namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class TwitchCategoryOnStreamConfiguration : IEntityTypeConfiguration<TwitchCategoryOnStream>
    {
        public void Configure(EntityTypeBuilder<TwitchCategoryOnStream> builder)
        {
            builder.HasOne(tcos => tcos.Category)
                .WithMany(tc => tc.CategoriesOnStream)
                .HasForeignKey(tcos => tcos.TwitchCategoryId);

            builder.HasOne(tcos => tcos.Stream)
                .WithMany(s => s.CategoriesOnStream)
                .HasForeignKey(tcos => tcos.StreamId);
        }
    }
}
