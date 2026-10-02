using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MyStreamHistory.ContentService.Api;

public sealed class ContentDatabaseHealthCheck(ContentDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Content database is unavailable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Content database is unavailable.", exception);
        }
    }
}
