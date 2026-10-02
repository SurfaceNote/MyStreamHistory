using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Requests;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.Shared.Base.Exceptions;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class GetAllUsersQueryHandlerTests
{
    [Fact]
    public async Task GetsLiveStatusInOneBatchForTheEntireDirectory()
    {
        var users = Enumerable.Range(1, 150).Select(id => new UserDto
        {
            TwitchId = id, DisplayName = $"Streamer {id}", Avatar = $"avatar-{id}"
        }).ToList();
        var bus = new DirectoryBus(users, new GetLiveStreamersResponseContract
        {
            TwitchUserIds = [150, 999]
        });
        using var cancellation = new CancellationTokenSource();

        var result = await new GetAllUsersQueryHandler(bus).Handle(new GetAllUsersQuery(), cancellation.Token);

        Assert.Equal(150, result.Count);
        Assert.True(result[149].IsLive);
        Assert.All(result.Take(149), user => Assert.False(user.IsLive));
        Assert.Equal(users.Select(u => (u.TwitchId, u.DisplayName, u.Avatar)),
            result.Select(u => (u.TwitchId, u.DisplayName, u.Avatar)));
        Assert.Equal(new[] { typeof(GetAllUsersRequestContract), typeof(GetLiveStreamersRequestContract) }, bus.Requests);
        Assert.All(bus.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task EmptyDirectoryDoesNotRequestLiveStatus()
    {
        var bus = new DirectoryBus([], new GetLiveStreamersResponseContract());
        var result = await new GetAllUsersQueryHandler(bus).Handle(new GetAllUsersQuery(), default);
        Assert.Empty(result);
        Assert.Single(bus.Requests);
    }

    [Fact]
    public async Task LiveStatusFailureIsNotReportedAsEveryoneOffline()
    {
        var bus = new DirectoryBus([new UserDto { TwitchId = 1 }],
            new BaseFailedResponseContract { Reason = "Tracking unavailable" });
        await Assert.ThrowsAsync<AppException>(() =>
            new GetAllUsersQueryHandler(bus).Handle(new GetAllUsersQuery(), default));
    }

    private sealed class DirectoryBus(List<UserDto> users, object liveResponse) : ITransportBus
    {
        public List<Type> Requests { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<TransportResponse<TSuccess, TFailure>> SendRequestAsync<TRequest, TSuccess, TFailure>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class where TSuccess : class where TFailure : class
        {
            Requests.Add(typeof(TRequest));
            Tokens.Add(cancellationToken);
            object response = request switch
            {
                GetAllUsersRequestContract => new GetAllUsersResponseContract { Users = users },
                GetLiveStreamersRequestContract => liveResponse,
                _ => throw new InvalidOperationException("Unexpected per-channel request")
            };
            return Task.FromResult(new TransportResponse<TSuccess, TFailure>
            {
                Success = response as TSuccess,
                Failure = response as TFailure
            });
        }

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
            => throw new NotSupportedException();

        public Task<EmptyResponse> SendRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class => throw new NotSupportedException();

        public Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(
            TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class where TResponse : class => throw new NotSupportedException();
    }
}
