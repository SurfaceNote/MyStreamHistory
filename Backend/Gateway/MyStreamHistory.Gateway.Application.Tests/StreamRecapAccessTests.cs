using MyStreamHistory.Gateway.Application.Queries;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class StreamRecapAccessTests
{
    private const int Broadcaster = 42;

    [Fact]
    public void Broadcaster_CanReadOwnRecap() =>
        Assert.True(GetStreamRecapQuery.CanRead(new GetStreamRecapQuery { RequesterTwitchUserId = Broadcaster }, Broadcaster));

    [Fact]
    public void OtherUser_CannotReadRecap() =>
        Assert.False(GetStreamRecapQuery.CanRead(new GetStreamRecapQuery { RequesterTwitchUserId = 7 }, Broadcaster));

    [Fact]
    public void AnonymousRequest_CannotReadRecap() =>
        Assert.False(GetStreamRecapQuery.CanRead(new GetStreamRecapQuery(), Broadcaster));

    [Fact]
    public void AdminAccess_CanReadAnyRecap() =>
        Assert.True(GetStreamRecapQuery.CanRead(new GetStreamRecapQuery { AdminAccess = true }, Broadcaster));
}
