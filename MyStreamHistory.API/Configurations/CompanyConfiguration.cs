namespace MyStreamHistory.API.Configurations
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using MyStreamHistory.API.Models;

    public class CompanyConfiguration : IEntityTypeConfiguration<Company>
    {
        public void Configure(EntityTypeBuilder<Company> builder)
        {
            builder.HasIndex(s => s.IgdbId).IsUnique();
            builder.HasIndex(s => s.Slug).IsUnique();
        }
    }
}
