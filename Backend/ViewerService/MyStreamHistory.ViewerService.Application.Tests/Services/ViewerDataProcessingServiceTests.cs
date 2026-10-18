using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.ViewerService.Application.DTOs;
using MyStreamHistory.ViewerService.Application.Interfaces;
using MyStreamHistory.ViewerService.Application.Services;
using MyStreamHistory.ViewerService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.ViewerService.Application.Tests.Services;

public class ViewerDataProcessingServiceTests
{
    [Fact]
    public async Task ProcessSnapshotAsync_NeverSyncedViewerWithoutProfileChanges_PersistsLastDataSyncAtAndSkipsRepeatSync()
    {
        var viewer = CreateViewer(lastDataSyncAt: null);
        var viewerRepository = new FakeViewerRepository(viewer);
        var usersApiClient = new FakeTwitchUsersApiClient(CreateTwitchUserFrom(viewer));
        var service = CreateService(viewerRepository, usersApiClient);

        var beforeSync = DateTime.UtcNow;
        await service.ProcessSnapshotAsync(CreateSnapshot());
        var afterSync = DateTime.UtcNow;

        Assert.Single(viewerRepository.BulkUpdatedViewers);
        Assert.InRange(viewer.LastDataSyncAt!.Value, beforeSync, afterSync);
        Assert.Equal(1, usersApiClient.CallCount);

        await service.ProcessSnapshotAsync(CreateSnapshot());

        Assert.Single(viewerRepository.BulkUpdatedViewers);
        Assert.Equal(1, usersApiClient.CallCount);
    }

    [Fact]
    public async Task ProcessSnapshotAsync_ExpiredViewerWithoutProfileChanges_PersistsNewLastDataSyncAt()
    {
        var oldSyncTimestamp = DateTime.UtcNow.AddDays(-8);
        var viewer = CreateViewer(oldSyncTimestamp);
        var viewerRepository = new FakeViewerRepository(viewer);
        var usersApiClient = new FakeTwitchUsersApiClient(CreateTwitchUserFrom(viewer));
        var service = CreateService(viewerRepository, usersApiClient);

        await service.ProcessSnapshotAsync(CreateSnapshot());

        Assert.Single(viewerRepository.BulkUpdatedViewers);
        Assert.True(viewer.LastDataSyncAt > oldSyncTimestamp);
        Assert.Equal(1, usersApiClient.CallCount);
    }

    [Fact]
    public async Task MissingCategory_RecoversSameSessionAndPersistsMessagePointsInCurrentAndFutureSnapshots()
    {
        var viewer = CreateViewer(DateTime.UtcNow);
        var buffer = new ChatMessageBufferService();
        var session = Guid.NewGuid();
        var category = Guid.NewGuid();
        buffer.InitializeStream("streamer-1", session, null);
        buffer.AddChatMessage("streamer-1", viewer.TwitchUserId, 280);
        var categories = new FakeStreamCategoryService
        {
            Result = new() { StreamSessionId = session, StreamCategoryId = category, IsLiveConfirmed = true }
        };
        var watches = new FakeViewerCategoryWatchRepository();
        var stats = new FakeViewerStatsRepository();
        var service = CreateService(new(viewer), new(CreateTwitchUserFrom(viewer)), categories, buffer, watches, stats);

        await service.ProcessSnapshotAsync(buffer.CreateSnapshot());

        Assert.Equal(category, Assert.Single(watches.Deltas).StreamCategoryId);
        Assert.Equal(2m, Assert.Single(watches.Deltas).ChatPoints);
        Assert.Equal(2m, Assert.Single(stats.Deltas).EarnedMsgPoints);
        Assert.Equal(1, Assert.Single(stats.Deltas).MinutesWatched);

        buffer.AddChatMessage("streamer-1", viewer.TwitchUserId, 140);
        await service.ProcessSnapshotAsync(buffer.CreateSnapshot());

        Assert.Equal(1, categories.CallCount);
        Assert.Equal(3m, stats.Deltas.Sum(x => x.EarnedMsgPoints));
        Assert.All(watches.Deltas, x => Assert.Equal(category, x.StreamCategoryId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingCategory_UnavailableOrFailedLookup_StillSavesOverallStatsAndRetriesNextSnapshot(bool failLookup)
    {
        var viewer = CreateViewer(DateTime.UtcNow);
        var buffer = new ChatMessageBufferService();
        var session = Guid.NewGuid();
        buffer.InitializeStream("streamer-1", session, null);
        buffer.AddChatMessage("streamer-1", viewer.TwitchUserId, 140);
        var categories = new FakeStreamCategoryService { FailLookup = failLookup };
        var watches = new FakeViewerCategoryWatchRepository();
        var stats = new FakeViewerStatsRepository();
        var service = CreateService(new(viewer), new(CreateTwitchUserFrom(viewer)), categories, buffer, watches, stats);

        await service.ProcessSnapshotAsync(buffer.CreateSnapshot());

        Assert.Empty(watches.Deltas);
        Assert.Equal(1m, Assert.Single(stats.Deltas).EarnedMsgPoints);
        Assert.Equal(1, Assert.Single(stats.Deltas).MinutesWatched);

        categories.FailLookup = false;
        var category = Guid.NewGuid();
        categories.Result = new() { StreamSessionId = session, StreamCategoryId = category, IsLiveConfirmed = true };
        await service.ProcessSnapshotAsync(buffer.CreateSnapshot());

        Assert.Equal(2, categories.CallCount);
        Assert.Equal(category, Assert.Single(watches.Deltas).StreamCategoryId);
        Assert.Equal(2, stats.Deltas.Count);
    }

    [Fact]
    public async Task MissingCategory_LookupReturnsDifferentSession_DoesNotAssignItsCategory()
    {
        var viewer = CreateViewer(DateTime.UtcNow);
        var buffer = new ChatMessageBufferService();
        buffer.InitializeStream("streamer-1", Guid.NewGuid(), null);
        var categories = new FakeStreamCategoryService
        {
            Result = new() { StreamSessionId = Guid.NewGuid(), StreamCategoryId = Guid.NewGuid(), IsLiveConfirmed = true }
        };
        var watches = new FakeViewerCategoryWatchRepository();
        var stats = new FakeViewerStatsRepository();
        var service = CreateService(new(viewer), new(CreateTwitchUserFrom(viewer)), categories, buffer, watches, stats);

        await service.ProcessSnapshotAsync(buffer.CreateSnapshot());

        Assert.Empty(watches.Deltas);
        Assert.Single(stats.Deltas);
        Assert.Null(buffer.CreateSnapshot().StreamSnapshots["streamer-1"].CurrentCategoryId);
    }

    private static ViewerDataProcessingService CreateService(
        FakeViewerRepository viewerRepository,
        FakeTwitchUsersApiClient usersApiClient,
        FakeStreamCategoryService? categoryService = null,
        ChatMessageBufferService? bufferService = null,
        FakeViewerCategoryWatchRepository? watches = null,
        FakeViewerStatsRepository? stats = null)
    {
        return new ViewerDataProcessingService(
            new FakeTwitchChatApiClient(),
            usersApiClient,
            new FakeAuthTokenService(),
            viewerRepository,
            watches ?? new FakeViewerCategoryWatchRepository(),
            stats ?? new FakeViewerStatsRepository(),
            NullLogger<ViewerDataProcessingService>.Instance,
            categoryService ?? new FakeStreamCategoryService(),
            bufferService ?? new ChatMessageBufferService());
    }

    private static DataCollectionSnapshot CreateSnapshot()
    {
        return new DataCollectionSnapshot
        {
            Timestamp = DateTime.UtcNow,
            StreamSnapshots =
            {
                ["streamer-1"] = new StreamDataSnapshot
                {
                    TwitchUserId = "streamer-1",
                    StreamSessionId = Guid.NewGuid(),
                    CurrentCategoryId = Guid.NewGuid()
                }
            }
        };
    }

    private static Viewer CreateViewer(DateTime? lastDataSyncAt)
    {
        return new Viewer
        {
            Id = Guid.NewGuid(),
            TwitchUserId = "viewer-1",
            Login = "viewer_login",
            DisplayName = "ViewerName",
            ProfileImageUrl = "https://static-cdn.jtvnw.net/viewer.png",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastDataSyncAt = lastDataSyncAt
        };
    }

    private static TwitchUserDto CreateTwitchUserFrom(Viewer viewer)
    {
        return new TwitchUserDto
        {
            Id = viewer.TwitchUserId,
            Login = viewer.Login,
            DisplayName = viewer.DisplayName,
            ProfileImageUrl = viewer.ProfileImageUrl ?? string.Empty
        };
    }

    private sealed class FakeStreamCategoryService : IStreamCategoryService
    {
        public ActiveStreamCategoryDto? Result { get; set; }
        public bool FailLookup { get; set; }
        public int CallCount { get; private set; }

        public Task<ActiveStreamCategoryDto?> GetActiveStreamCategoryAsync(string twitchUserId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (FailLookup) throw new HttpRequestException("Tracking service unavailable");
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAuthTokenService : IAuthTokenService
    {
        public Task<(string AccessToken, DateTime ExpiresAt)?> GetTwitchAccessTokenAsync(
            string twitchUserId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<(string, DateTime)?>(("access-token", DateTime.UtcNow.AddHours(1)));
        }

        public Task<(string AccessToken, DateTime ExpiresAt)?> RefreshTwitchAccessTokenAsync(
            string twitchUserId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeTwitchChatApiClient : ITwitchChatApiClient
    {
        public Task<List<TwitchChatterDto>> GetChattersAsync(
            string broadcasterId,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<TwitchChatterDto>
            {
                new()
                {
                    UserId = "viewer-1",
                    UserLogin = "viewer_login",
                    UserName = "ViewerName"
                }
            });
        }
    }

    private sealed class FakeTwitchUsersApiClient(TwitchUserDto twitchUser) : ITwitchUsersApiClient
    {
        public int CallCount { get; private set; }

        public Task<List<TwitchUserDto>> GetUsersByIdsAsync(
            List<string> userIds,
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(userIds.Contains(twitchUser.Id)
                ? new List<TwitchUserDto> { twitchUser }
                : []);
        }
    }

    private sealed class FakeViewerRepository(Viewer viewer) : IViewerRepository
    {
        public List<Viewer> BulkUpdatedViewers { get; } = [];

        public Task<Viewer?> GetByTwitchUserIdAsync(string twitchUserId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Viewer?>(viewer.TwitchUserId == twitchUserId ? viewer : null);
        }

        public Task<Viewer> AddAsync(Viewer viewer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<Viewer>> GetOrCreateViewersAsync(
            List<(string TwitchUserId, string Login, string DisplayName)> viewers,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<Viewer> { viewer });
        }

        public Task UpdateAsync(Viewer viewer, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkUpdateAsync(List<Viewer> viewers, CancellationToken cancellationToken = default)
        {
            BulkUpdatedViewers.AddRange(viewers);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeViewerCategoryWatchRepository : IViewerCategoryWatchRepository
    {
        public List<ViewerCategoryWatch> Deltas { get; } = [];

        public Task<ViewerCategoryWatch?> GetByViewerAndCategoryAsync(
            Guid viewerId,
            Guid streamCategoryId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerCategoryWatch>> GetByViewerIdsAndCategoryAsync(
            List<Guid> viewerIds,
            Guid streamCategoryId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerCategoryWatch>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdAsync(
            Guid streamCategoryId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerCategoryWatch>> GetByStreamCategoryIdsAsync(
            List<Guid> streamCategoryIds,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<MyStreamHistory.Shared.Base.Contracts.Viewers.UniqueViewerCountDto>> GetUniqueViewerCountsAsync(
            IReadOnlyCollection<UniqueViewerCountQueryDto> playthroughs,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkUpsertAsync(List<ViewerCategoryWatch> watches, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkIncrementAsync(List<ViewerCategoryWatch> watchDeltas, CancellationToken cancellationToken = default)
        {
            Deltas.AddRange(watchDeltas);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeViewerStatsRepository : IViewerStatsRepository
    {
        public List<ViewerStats> Deltas { get; } = [];

        public Task<ViewerStats?> GetByViewerAndStreamerAsync(
            Guid viewerId,
            string streamerTwitchUserId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<ViewerStats?> GetByViewerTwitchIdAndStreamerAsync(
            string viewerTwitchUserId,
            string streamerTwitchUserId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerStats>> GetByViewerIdsAndStreamerAsync(
            List<Guid> viewerIds,
            string streamerTwitchUserId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerStats>> GetTopViewersByStreamerAsync(
            string streamerTwitchUserId,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<List<ViewerStats>> GetByViewerIdAsync(Guid viewerId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkUpsertAsync(List<ViewerStats> stats, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkIncrementAsync(List<ViewerStats> statDeltas, CancellationToken cancellationToken = default)
        {
            Deltas.AddRange(statDeltas);
            return Task.CompletedTask;
        }
    }
}
