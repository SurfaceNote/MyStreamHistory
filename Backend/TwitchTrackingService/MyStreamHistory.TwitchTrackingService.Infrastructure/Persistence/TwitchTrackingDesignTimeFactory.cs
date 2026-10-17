using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

public class TwitchTrackingDesignTimeFactory : IDesignTimeDbContextFactory<TwitchTrackingDbContext>
{
    public TwitchTrackingDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("MYSTREAMHISTORY_DESIGN_CONNECTION")
            ?? "Host=localhost;Database=mystreamhistory_design;Username=postgres").Options);
}
