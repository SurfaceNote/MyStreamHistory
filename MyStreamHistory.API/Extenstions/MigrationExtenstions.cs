namespace MyStreamHistory.API.Extenstions
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Data;
    public static class MigrationExtenstions
    {
        public static void ApplyMigrations(this IApplicationBuilder app)
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            using AppDbContext dbContext =
                scope.ServiceProvider.GetRequiredService<AppDbContext>();

            dbContext.Database.Migrate();
        }
    }
}
