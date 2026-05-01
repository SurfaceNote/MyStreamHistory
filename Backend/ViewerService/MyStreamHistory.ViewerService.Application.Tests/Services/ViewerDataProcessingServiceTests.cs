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

    private static ViewerDataProcessingService CreateService(
        FakeViewerRepository viewerRepository,
        FakeTwitchUsersApiClient usersApiClient)
    {
        return new ViewerDataProcessingService(
            new FakeTwitchChatApiClient(),
            usersApiClient,
            new FakeAuthTokenService(),
            viewerRepository,
            new FakeViewerCategoryWatchRepository(),
            new FakeViewerStatsRepository(),
            NullLogger<ViewerDataProcessingService>.Instance);
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

        public Task BulkUpsertAsync(List<ViewerCategoryWatch> watches, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task BulkIncrementAsync(List<ViewerCategoryWatch> watchDeltas, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeViewerStatsRepository : IViewerStatsRepository
    {
        public Task<ViewerStats?> GetByViewerAndStreamerAsync(
            Guid viewerId,
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
            return Task.CompletedTask;
        }
    }
}
