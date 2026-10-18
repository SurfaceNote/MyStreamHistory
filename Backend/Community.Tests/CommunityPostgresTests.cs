using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Community;
using MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Domain.Entities;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;
using MyStreamHistory.ViewerService.Infrastructure.Services;
using MyStreamHistory.ViewerService.Infrastructure.Options;
using Npgsql;
using Xunit;

namespace MyStreamHistory.Community.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("COMMUNITY_TEST_POSTGRES")))
            Skip = "Set COMMUNITY_TEST_POSTGRES to an isolated PostgreSQL test server. A fresh temporary database is created per test.";
    }
}

public class CommunityPostgresTests
{
    [PostgresFact]
    public async Task MigrationsAndConcurrentPollsAreIdempotentAndPartialPagesCanBeUpgraded()
    {
        await using var fixture = await Database.Create();
        var session = Guid.NewGuid();
        var at = DateTime.UtcNow;
        await using (var db = fixture.Viewers())
            await Service(db).RecordPollAsync("10", session, at, null, default);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = fixture.Viewers();
            await Service(db).RecordPollAsync("10", session, at, [Chatter("1"), Chatter("1"), Chatter("2")], default);
        }));
        await using var check = fixture.Viewers();
        Assert.Single(await check.CommunityPolls.ToListAsync());
        Assert.True((await check.CommunityPolls.SingleAsync()).Complete);
        Assert.Equal(2, await check.CommunityPresences.CountAsync());
        await Service(check).RecordPollAsync("10", session, at, null, default);
        Assert.True((await check.CommunityPolls.AsNoTracking().SingleAsync()).Complete);
    }

    [PostgresFact]
    public async Task MessageWithoutPresenceIsCountedOnceAndForeignSharedChatIsExcluded()
    {
        await using var fixture = await Database.Create();
        var session = Guid.NewGuid();
        var message = Message();
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = fixture.Viewers();
            await Service(db).RecordMessageAsync(message, session, default);
        }));
        await using var check = fixture.Viewers();
        Assert.Empty(await check.CommunityPresences.ToListAsync());
        Assert.Equal(1, (await check.CommunityMessageAggregates.SingleAsync()).Count);
        Assert.False((await check.CommunityMessageAggregates.SingleAsync()).Provisional);
        Assert.Equal("Author", (await check.Viewers.SingleAsync()).DisplayName);
        message.TwitchMessageId = "foreign-message";
        message.SourceBroadcasterUserId = "999";
        await Service(check).RecordMessageAsync(message, session, default);
        Assert.Equal(1, await check.CommunityMessageReceipts.CountAsync());
        // Existing two-hour cleanup does not touch new receipt identifiers.
        await new ProcessedEventSubMessageRepository(check).DeleteOlderThanAsync(DateTime.UtcNow.AddDays(1));
        message = Message();
        await Service(check).RecordMessageAsync(message, session, default);
        Assert.Equal(1, (await check.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);
    }

    [PostgresFact]
    public async Task ReceiptAndAggregateRollbackTogether()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        var session = Guid.NewGuid();
        db.CommunityMessageAggregates.Add(new() { StreamerTwitchUserId = "10", StreamSessionId = session, ViewerTwitchUserId = "1", Count = int.MaxValue });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => Service(db).RecordMessageAsync(Message(), session, default));
        Assert.Empty(await db.CommunityMessageReceipts.AsNoTracking().ToListAsync());
        Assert.Equal(int.MaxValue, (await db.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);
    }

    [PostgresFact]
    public async Task ExpiredReceiptsAreRemovedWithoutChangingCountsOrRecentDeduplication()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        var session = Guid.NewGuid();
        db.CommunityMessageReceipts.Add(new()
        {
            StreamerTwitchUserId = "10", MessageId = "expired", ReceivedAt = DateTime.UtcNow.AddDays(-8)
        });
        db.CommunityMessageAggregates.Add(new()
        {
            StreamerTwitchUserId = "10", StreamSessionId = session, ViewerTwitchUserId = "1", Count = 5
        });
        await db.SaveChangesAsync();
        var service = Service(db);
        await service.RecordMessageAsync(Message(), session, default);

        Assert.Equal(1, await service.DeleteExpiredMessageReceiptsAsync(default));
        var receipt = Assert.Single(await db.CommunityMessageReceipts.AsNoTracking().ToListAsync());
        Assert.Equal("twitch-message-1", receipt.MessageId);
        Assert.Equal(6, (await db.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);

        await service.RecordMessageAsync(Message(), session, default);
        Assert.Equal(6, (await db.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);
        Assert.Equal(0, await service.DeleteExpiredMessageReceiptsAsync(default));
    }

    [PostgresFact]
    public async Task ReplayOutsideRetentionWindowDoesNotRecountAfterReceiptWasDeleted()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        var session = Guid.NewGuid();
        var message = Message();
        message.MessageTimestamp = DateTime.UtcNow.AddDays(-8);
        db.CommunityMessageReceipts.Add(new()
        {
            StreamerTwitchUserId = "10", MessageId = message.TwitchMessageId!, ReceivedAt = message.MessageTimestamp
        });
        db.CommunityMessageAggregates.Add(new()
        {
            StreamerTwitchUserId = "10", StreamSessionId = session, ViewerTwitchUserId = "1", Count = 1
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        Assert.Equal(1, await service.DeleteExpiredMessageReceiptsAsync(default));
        await service.RecordMessageAsync(message, session, default);

        Assert.Empty(await db.CommunityMessageReceipts.AsNoTracking().ToListAsync());
        Assert.Equal(1, (await db.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);
    }

    [PostgresFact]
    public async Task CustomRetentionCleansMultipleBatchesEvenWhenAnalyticsIsDisabled()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        db.CommunityMessageReceipts.AddRange(Enumerable.Range(0, 1001).Select(i => new CommunityMessageReceipt
        {
            StreamerTwitchUserId = "10", MessageId = "old-" + i, ReceivedAt = DateTime.UtcNow.AddDays(-2)
        }));
        db.CommunityMessageReceipts.Add(new()
        {
            StreamerTwitchUserId = "10", MessageId = "recent", ReceivedAt = DateTime.UtcNow.AddHours(-12)
        });
        await db.SaveChangesAsync();

        var deleted = await Service(db, retentionDays: 1, enabled: false).DeleteExpiredMessageReceiptsAsync(default);

        Assert.Equal(1001, deleted);
        Assert.Equal("recent", (await db.CommunityMessageReceipts.AsNoTracking().SingleAsync()).MessageId);
    }

    [PostgresFact]
    public async Task CustomRetentionAlsoAppliesToIncomingMessages()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        var service = Service(db, retentionDays: 1);
        var message = Message();
        message.MessageTimestamp = DateTime.UtcNow.AddDays(-2);

        await service.RecordMessageAsync(message, Guid.NewGuid(), default);

        Assert.Empty(await db.CommunityMessageReceipts.ToListAsync());
        Assert.Empty(await db.CommunityMessageAggregates.ToListAsync());

        message.MessageTimestamp = DateTime.UtcNow.AddHours(-12);
        await service.RecordMessageAsync(message, Guid.NewGuid(), default);
        Assert.Equal(1, (await db.CommunityMessageAggregates.SingleAsync()).Count);
    }

    [PostgresFact]
    public async Task ReceiptCompactionMigrationPreservesExistingKeysAndAggregates()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Viewers();
        await db.GetService<IMigrator>().MigrateAsync("20261017180125_AddCommunityAnalytics");
        var session = Guid.NewGuid();
        var received = DateTime.UtcNow.AddHours(-1);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CommunityMessageReceipts"
                ("StreamerTwitchUserId", "MessageId", "StreamSessionId", "ViewerTwitchUserId", "ReceivedAt", "Provisional")
            VALUES ('10', 'twitch-message-1', {session}, '1', {received}, FALSE)
            """);
        db.CommunityMessageAggregates.Add(new()
        {
            StreamerTwitchUserId = "10", StreamSessionId = session, ViewerTwitchUserId = "1", Count = 7
        });
        await db.SaveChangesAsync();

        await db.Database.MigrateAsync();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal("twitch-message-1", (await db.CommunityMessageReceipts.SingleAsync()).MessageId);
        await Service(db).RecordMessageAsync(Message(), session, default);
        Assert.Equal(7, (await db.CommunityMessageAggregates.AsNoTracking().SingleAsync()).Count);
    }

    [PostgresFact]
    public async Task HistoricalImportIsRepeatableAndSettingsInvalidateCachedReport()
    {
        await using var fixture = await Database.Create();
        var metadata = new FakeBus();
        var user = new Viewer { Id = Guid.NewGuid(), TwitchUserId = "1", Login = "quiet", DisplayName = "Quiet", CreatedAt = DateTime.UtcNow };
        await using var db = fixture.Viewers();
        db.Viewers.Add(user);
        for (var i = 0; i < 10; i++)
        {
            var category = Guid.NewGuid();
            metadata.Sessions.Add(new() { Id = Guid.NewGuid(), StartedAt = DateTime.UtcNow.Date.AddDays(-2 - i * 2), EndedAt = DateTime.UtcNow.Date.AddDays(-2 - i * 2).AddHours(1), CategoryIds = [category] });
            db.ViewerCategoryWatches.Add(new() { Id = Guid.NewGuid(), ViewerId = user.Id, StreamCategoryId = category, MinutesWatched = 10, LastUpdatedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
        var service = Service(db, metadata);
        await service.HandleAsync(new() { StreamerTwitchUserId = "10" }, default);
        await service.RecalculatePendingAsync(default);
        var first = await service.HandleAsync(new() { StreamerTwitchUserId = "10" }, default);
        Assert.Equal("provisional", first.Report!.Quality);
        Assert.Equal(1, first.Report.Core);
        var states = await db.CommunityDailyStates.CountAsync();
        await db.CommunitySettings.ExecuteUpdateAsync(s => s.SetProperty(x => x.Pending, true));
        await service.RecalculatePendingAsync(default);
        Assert.Equal(states, await db.CommunityDailyStates.CountAsync());
        var excluded = await service.HandleAsync(new() { StreamerTwitchUserId = "10", Operation = "exclude", ViewerTwitchUserId = "1" }, default);
        Assert.True(excluded.Report!.Recalculating);
        Assert.Null(excluded.Report.Core);
        await service.RecalculatePendingAsync(default);
        var after = await service.HandleAsync(new() { StreamerTwitchUserId = "10" }, default);
        Assert.Equal(0, after.Report!.Core);
        Assert.Empty(after.Report.Transitions);
        Assert.Null((await service.HandleAsync(new() { StreamerTwitchUserId = "10", Operation = "settings", TimeZoneId = "Europe/Moscow" }, default)).Error);
        Assert.Equal("invalidTimeZone", (await service.HandleAsync(new() { StreamerTwitchUserId = "10", Operation = "settings", TimeZoneId = "not/a/timezone" }, default)).Error);
    }

    [PostgresFact]
    public async Task AudienceMigrationsPreserveLastValueAndStoreDistinctSamples()
    {
        await using var fixture = await Database.Create();
        await using var db = fixture.Tracking();
        await db.Database.MigrateAsync();
        var session = new StreamSession { TwitchUserId = 10, StreamerLogin = "demo", StreamerDisplayName = "Demo", StartedAt = DateTime.UtcNow, IsLive = true, ViewerCount = 42 };
        db.StreamSessions.Add(session);
        await db.SaveChangesAsync();
        var repo = new StreamSessionRepository(db);
        var at = DateTime.UtcNow;
        await repo.RecordAudienceSampleAsync(session.Id, at, 42);
        await repo.RecordAudienceSampleAsync(session.Id, at, 99);
        await repo.RecordAudienceSampleAsync(session.Id, at.AddMinutes(1), 43);
        Assert.Equal(2, await db.ViewerCountSamples.CountAsync());
        Assert.Equal(42, (await db.StreamSessions.AsNoTracking().SingleAsync()).ViewerCount);
    }

    private static TwitchChatterDto Chatter(string id) => new() { UserId = id, UserLogin = "user" + id, UserName = "User " + id };
    private static ChatMessageEventContract Message() => new()
    {
        BroadcasterUserId = "10", ChatterUserId = "1", ChatterUserLogin = "author", ChatterUserName = "Author",
        MessageId = "delivery-1", TwitchMessageId = "twitch-message-1", HasSourceMetadata = true, MessageTimestamp = DateTime.UtcNow
    };
    internal static CommunityService Service(ViewerServiceDbContext db, FakeBus? bus = null, int retentionDays = 7, bool enabled = true) => new(db, bus ?? new FakeBus(),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Features:CommunityAnalytics:Enabled"] = enabled.ToString() }).Build(),
        new EnvironmentStub(), NullLogger<CommunityService>.Instance,
        Options.Create(new CommunityAnalyticsOptions { MessageReceiptRetentionDays = retentionDays }));

    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class Database(string connectionString) : IAsyncDisposable
    {
        public static async Task<Database> Create()
        {
            var builder = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COMMUNITY_TEST_POSTGRES"));
            builder.Pooling = false;
            var name = "msh_community_test_" + Guid.NewGuid().ToString("N");
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE {name}", connection);
            await command.ExecuteNonQueryAsync();
            builder.Database = name;
            var fixture = new Database(builder.ConnectionString);
            await using var db = fixture.Viewers();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            return fixture;
        }
        public ViewerServiceDbContext Viewers() => new(new DbContextOptionsBuilder<ViewerServiceDbContext>().UseNpgsql(connectionString).Options);
        public TwitchTrackingDbContext Tracking() => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>().UseNpgsql(connectionString).Options);
        public async ValueTask DisposeAsync()
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var name = builder.Database;
            builder.Database = "postgres";
            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}

public sealed class FakeBus : ITransportBus
{
    public List<CommunitySession> Sessions { get; } = [];
    public CommunityRequest? LastRequest { get; private set; }
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    public Task<EmptyResponse> SendRequestAsync<T>(T request, CancellationToken cancellationToken = default) where T : class => throw new NotSupportedException();
    public Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default) where TRequest : class where TResponse : class
    {
        object response;
        if (request is GetCommunitySessionsRequest) response = new GetCommunitySessionsResponse { Sessions = Sessions };
        else { LastRequest = (CommunityRequest)(object)request; response = new CommunityResponse { Report = new() }; }
        return Task.FromResult(new TransportResponse<TResponse> { Success = (TResponse)response });
    }
    public Task<TransportResponse<TSuccess, TFailure>> SendRequestAsync<TRequest, TSuccess, TFailure>(TRequest request, CancellationToken cancellationToken = default) where TRequest : class where TSuccess : class where TFailure : class => throw new NotSupportedException();
}
