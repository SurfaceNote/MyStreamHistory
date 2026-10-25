using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Requests;
using MyStreamHistory.Shared.Base.Contracts.StreamSessions.Responses;
using MyStreamHistory.Shared.Base.Contracts.Users;
using MyStreamHistory.Shared.Base.Contracts.Users.Response;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.TwitchTrackingService.Application.Interfaces;
using MyStreamHistory.TwitchTrackingService.Application.Services;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence.Repositories;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class CurrentStreamerProfileTests
{
    [Theory]
    [InlineData("current", "new_login", "New Name", "https://img.test/new.png")]
    [InlineData("legacy", "old_login", "New Name", "https://img.test/new.png")]
    [InlineData("partial", "new_login", "Old Name", "https://img.test/old.png")]
    [InlineData("unavailable", "old_login", "Old Name", "https://img.test/old.png")]
    [InlineData("missing", "old_login", "Old Name", "https://img.test/old.png")]
    public async Task RecentStreamsUseOneCurrentProfileRead_AndDoNotRewriteHistory(
        string mode, string expectedLogin, string expectedName, string expectedAvatar)
    {
        await using var db = CreateDb();
        db.StreamSessions.AddRange(Session(), Session());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (profiles, bus) = Profiles(mode);
        var service = Service(db, profiles);

        var result = await service.GetRecentStreamsByTwitchUserIdAsync(123);

        Assert.Equal(2, result.Count);
        Assert.All(result, session =>
        {
            Assert.Equal(expectedLogin, session.StreamerLogin);
            Assert.Equal(expectedName, session.StreamerDisplayName);
            Assert.Equal(expectedAvatar, session.StreamerAvatarUrl);
        });
        Assert.Equal(1, bus.Calls);
        Assert.All(await db.StreamSessions.AsNoTracking().ToListAsync(), stored => Assert.Equal("old_login", stored.StreamerLogin));
    }

    [Fact]
    public async Task SingleSessionApplicationReadUsesCurrentProfile()
    {
        await using var db = CreateDb();
        var stored = Session();
        db.StreamSessions.Add(stored);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (profiles, bus) = Profiles("current");

        var result = await Service(db, profiles).GetStreamSessionByIdAsync(stored.Id);

        Assert.NotNull(result);
        Assert.Equal("new_login", result.StreamerLogin);
        Assert.Equal("New Name", result.StreamerDisplayName);
        Assert.Equal("https://img.test/new.png", result.StreamerAvatarUrl);
        Assert.Equal(1, bus.Calls);
    }

    [Theory]
    [InlineData("current", "new_login", "New Name", "https://img.test/new.png")]
    [InlineData("legacy", "old_login", "New Name", "https://img.test/new.png")]
    [InlineData("unavailable", "old_login", "Old Name", "https://img.test/old.png")]
    public async Task StreamDetailsConsumerUsesCurrentProfileIncludingLogin(
        string mode, string expectedLogin, string expectedName, string expectedAvatar)
    {
        await using var db = CreateDb();
        var stored = Session();
        db.StreamSessions.Add(stored);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var (profiles, bus) = Profiles(mode);
        var consumer = new GetStreamSessionByIdConsumer(new StreamSessionRepository(db), profiles, db,
            NullLogger<GetStreamSessionByIdConsumer>.Instance);
        var context = DispatchProxy.Create<ConsumeContext<GetStreamSessionByIdRequestContract>, DetailsContext>();
        var capture = (DetailsContext)(object)context;
        capture.Message = new GetStreamSessionByIdRequestContract { StreamSessionId = stored.Id };

        await consumer.Consume(context);

        var response = Assert.IsType<GetStreamSessionByIdResponseContract>(capture.Response);
        Assert.True(response.Success, response.Error);
        Assert.Equal(expectedLogin, response.StreamSession!.StreamerLogin);
        Assert.Equal(expectedName, response.StreamSession.StreamerDisplayName);
        Assert.Equal(expectedAvatar, response.StreamSession.StreamerAvatarUrl);
        Assert.Equal(1, bus.Calls);
        Assert.Equal("old_login", (await db.StreamSessions.SingleAsync()).StreamerLogin);
    }

    [Fact]
    public async Task EmptyOrUnknownSessionDoesNotRequestProfile()
    {
        await using var db = CreateDb();
        var (profiles, bus) = Profiles("unavailable");
        var service = Service(db, profiles);
        Assert.Empty(await service.GetRecentStreamsByTwitchUserIdAsync(123));
        Assert.Null(await service.GetStreamSessionByIdAsync(Guid.NewGuid()));
        Assert.Equal(0, bus.Calls);
    }

    private static StreamSessionService Service(TwitchTrackingDbContext db, IUserProfileService profiles)
        => new(new StreamSessionRepository(db), null!, null!, null!, profiles, null!, null!, NullLogger<StreamSessionService>.Instance);

    private static TwitchTrackingDbContext CreateDb() => new(new DbContextOptionsBuilder<TwitchTrackingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static StreamSession Session() => new()
    {
        TwitchUserId = 123, StreamerLogin = "old_login", StreamerDisplayName = "Old Name",
        StreamerAvatarUrl = "https://img.test/old.png", StartedAt = DateTime.UtcNow.AddDays(-2),
        EndedAt = DateTime.UtcNow.AddDays(-2).AddHours(2)
    };

    private static (UserProfileService Profiles, ProfileBus Bus) Profiles(string mode)
    {
        var bus = DispatchProxy.Create<ITransportBus, ProfileBus>();
        var stub = (ProfileBus)(object)bus;
        stub.Mode = mode;
        return (new UserProfileService(bus, NullLogger<UserProfileService>.Instance), stub);
    }

    public class ProfileBus : DispatchProxy
    {
        public string Mode { get; set; } = "current";
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != "SendRequestAsync" || targetMethod.GetGenericArguments().Length != 2)
                throw new NotSupportedException(targetMethod?.Name);
            Calls++;
            if (Mode == "unavailable")
                return Task.FromException<TransportResponse<GetUserByTwitchIdResponseContract>>(new HttpRequestException("AuthService unavailable"));
            return Task.FromResult(new TransportResponse<GetUserByTwitchIdResponseContract>
            {
                Success = Mode == "missing" ? null : new GetUserByTwitchIdResponseContract
                {
                    User = new UserDto
                    {
                        TwitchId = 123, Login = Mode == "legacy" ? null : "new_login",
                        DisplayName = Mode == "partial" ? " " : "New Name",
                        Avatar = Mode == "partial" ? "" : "https://img.test/new.png"
                    }
                }
            });
        }
    }

    public class DetailsContext : DispatchProxy
    {
        public GetStreamSessionByIdRequestContract Message { get; set; } = null!;
        public object? Response { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case "get_Message": return Message;
                case "get_CancellationToken": return CancellationToken.None;
                case "RespondAsync": Response = args![0]; return Task.CompletedTask;
                default: throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
