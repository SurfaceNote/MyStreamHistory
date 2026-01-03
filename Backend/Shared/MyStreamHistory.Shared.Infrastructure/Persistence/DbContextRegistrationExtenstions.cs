using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyStreamHistory.Shared.Infrastructure.Persistence
{
    public static class DbContextRegistrationExtenstions
    {
        public static InfrastructureBuilder AddDbContext<TDbContext>(
            this InfrastructureBuilder builder,
            string connectionStringName = "Default")
            where TDbContext : DbContext
        {
            builder.Services.AddDbContext<TDbContext>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString(connectionStringName));
                
            });

            return builder;
        }
    }
}
