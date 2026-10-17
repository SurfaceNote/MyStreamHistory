using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyStreamHistory.ViewerService.Infrastructure.Persistence;

public class ViewerServiceDesignTimeFactory : IDesignTimeDbContextFactory<ViewerServiceDbContext>
{
    public ViewerServiceDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ViewerServiceDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("MYSTREAMHISTORY_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=mystreamhistory_design;Username=postgres").Options);
}
