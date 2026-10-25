using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.AuthService.Application.DTOs.Twitch;
using MyStreamHistory.AuthService.Application.Interfaces;
using MyStreamHistory.AuthService.Domain.Entities;
using MyStreamHistory.AuthService.Infrastructure.Persistence;
using MyStreamHistory.AuthService.Infrastructure.Services;
using Xunit;

namespace MyStreamHistory.AuthService.Tests;

public class TwitchProfileSyncServiceTests
{
    [Theory]
    [InlineData("Login")]
    [InlineData("DisplayName")]
    [InlineData("Avatar")]
    [InlineData("All")]
    [InlineData("None")]
    public async Task UpdatesOnlyChangedProfileColumns_AndPreservesAuthData(string changed)
    {
        var saves = new Saves();
        await using var db = CreateDb(saves);
        var user = User(1);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var before = db.Entry(user).CurrentValues.Clone();
        db.ChangeTracker.Clear();
        saves.Modified.Clear();
        var profile = new TwitchPublicUserDto
        {
            Id = "1",
            Login = changed is "Login" or "All" ? "new_login" : user.Login,
            DisplayName = changed is "DisplayName" or "All" ? "New Name" : user.DisplayName,
            ProfileImageUrl = changed is "Avatar" or "All" ? "https://img.test/new.png" : user.Avatar
        };

        await Sync(db, new Client(_ => [profile]));

        var stored = await db.Users.SingleAsync();
        Assert.Equal(profile.Login, stored.Login);
        Assert.Equal(profile.DisplayName, stored.DisplayName);
        Assert.Equal(profile.ProfileImageUrl, stored.Avatar);
        foreach (var property in db.Entry(stored).Properties.Where(p => p.Metadata.Name is not ("Login" or "DisplayName" or "Avatar")))
            Assert.Equal(before[property.Metadata.Name], property.CurrentValue);
        var expected = changed switch
        {
            "All" => new[] { "Avatar", "DisplayName", "Login" },
            "None" => [],
            _ => [changed]
        };
        Assert.Equal(expected, saves.Modified.Order().ToArray());
    }

    [Fact]
    public async Task BatchesAllUsers_AndContinuesAfterFailedBatch()
    {
        await using var db = CreateDb();
        db.Users.AddRange(Enumerable.Range(1, 201).Select(User));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var client = new Client(ids => ids.Contains(101)
            ? throw new HttpRequestException("Twitch unavailable")
            : ids.Select(Profile).ToArray());

        await Sync(db, client);

        Assert.Equal([100, 100, 1], client.Batches.Select(b => b.Length));
        Assert.Equal(201, client.Batches.SelectMany(b => b).Distinct().Count());
        Assert.Equal(101, await db.Users.CountAsync(u => u.Login == "new_login"));
        Assert.Equal("old101", (await db.Users.SingleAsync(u => u.TwitchId == 101)).Login);
    }

    [Fact]
    public async Task MissingInvalidAndUnexpectedProfiles_PreserveStoredValues()
    {
        await using var db = CreateDb();
        db.Users.AddRange(Enumerable.Range(1, 5).Select(User));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var client = new Client(_ =>
        [
            new() { Id = "2", Login = "", DisplayName = "New", ProfileImageUrl = "https://img.test/new.png" },
            new() { Id = "3", Login = "new", DisplayName = "New", ProfileImageUrl = "invalid-url" },
            new() { Id = "4", Login = new string('x', 51), DisplayName = "New", ProfileImageUrl = "https://img.test/new.png" },
            Profile(5), Profile(999), new() { Id = "not-an-id" }, null!
        ]);

        await Sync(db, client);

        Assert.Equal(1, await db.Users.CountAsync(u => u.Login == "new_login"));
        Assert.Equal(4, await db.Users.CountAsync(u => u.Login.StartsWith("old")));
        Assert.Equal(5, await db.Users.CountAsync());
    }

    [Fact]
    public async Task EmptyDatabaseDoesNotRequestProfiles()
    {
        await using var db = CreateDb();
        var client = new Client(_ => throw new Exception("Unexpected request"));
        await Sync(db, client);
        Assert.Empty(client.Batches);
    }

    [Fact]
    public async Task CancellationIsPropagated_WithoutProcessingFurtherBatches()
    {
        await using var db = CreateDb();
        db.Users.Add(User(1));
        await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var client = new Client(_ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sync(db, client, cancellation.Token));
        Assert.Single(client.Batches);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static AuthDbContext CreateDb(Saves? saves = null)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (saves != null)
            options.AddInterceptors(saves);
        return new AuthDbContext(options.Options);
    }

    private static AuthUser User(int id) => new()
    {
        TwitchId = id, Login = $"old{id}", DisplayName = $"Old {id}", Avatar = "https://img.test/old.png",
        Email = "private@example.test", TwitchAccessToken = "access", TwitchRefreshToken = "refresh",
        IsTwitchTokenFresh = true, LastActivityAt = DateTime.UtcNow.AddDays(-3),
        LastLoginAt = DateTime.UtcNow.AddDays(-4), SiteCreatedAt = DateTime.UtcNow.AddDays(-5),
        TwitchCreatedAt = DateTime.UtcNow.AddYears(-1)
    };

    private static TwitchPublicUserDto Profile(int id) => new()
    {
        Id = id.ToString(), Login = "new_login", DisplayName = "New Name", ProfileImageUrl = "https://img.test/new.png"
    };

    private static Task Sync(AuthDbContext db, Client client, CancellationToken cancellationToken = default)
        => new TwitchProfileSyncService(db, client, NullLogger<TwitchProfileSyncService>.Instance).SyncAsync(cancellationToken);

    private sealed class Client(Func<int[], IReadOnlyList<TwitchPublicUserDto>> get) : ITwitchUsersApiClient
    {
        public List<int[]> Batches { get; } = [];
        public Task<IReadOnlyList<TwitchPublicUserDto>> GetUsersAsync(IReadOnlyCollection<int> twitchUserIds, CancellationToken cancellationToken = default)
        {
            var batch = twitchUserIds.ToArray();
            Batches.Add(batch);
            return Task.FromResult(get(batch));
        }
    }

    private sealed class Saves : SaveChangesInterceptor
    {
        public List<string> Modified { get; } = [];
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var db = eventData.Context!;
            db.ChangeTracker.DetectChanges();
            Modified.AddRange(db.ChangeTracker.Entries<AuthUser>().SelectMany(entry => entry.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name)));
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
