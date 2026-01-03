using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MyStreamHistory.Shared.Api.Extensions
{
    public static class AutoMigrationExtensions
    {
        public static void ApplyDatabaseMigrations<TDbContext>(this IApplicationBuilder app) where TDbContext : DbContext
        {
            using var scope = app.ApplicationServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            db.Database.Migrate();
        }
    }
}
