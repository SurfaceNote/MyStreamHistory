using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Games;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class GameQueriesTests
{
    [Fact]
    public void EmptyQueryStringGivesDefaults()
    {
        Assert.True(Parse(out var request));

        Assert.Null(request.Search);
        Assert.Null(request.Language);
        Assert.Equal(GamesCatalogFilter.All, request.Filter);
        Assert.Equal(GamesCatalogSort.Popular, request.Sort);
        Assert.Equal(1, request.Page);
        Assert.Equal(GamesCatalogRequestParser.DefaultPageSize, request.PageSize);
    }

    [Fact]
    public void ValuesAreTrimmedAndCaseInsensitive()
    {
        Assert.True(Parse(out var request, search: "  Hollow  ", language: " RU ", filter: "Completed", sort: "HOURS", page: 2, pageSize: 10));

        Assert.Equal("Hollow", request.Search);
        Assert.Equal("ru", request.Language);
        Assert.Equal(GamesCatalogFilter.Completed, request.Filter);
        Assert.Equal(GamesCatalogSort.Hours, request.Sort);
        Assert.Equal(2, request.Page);
        Assert.Equal(10, request.PageSize);
    }

    [Theory]
    [InlineData("zh-hk")]
    [InlineData("other")]
    public void AcceptsTwitchLanguageCodes(string language)
    {
        Assert.True(Parse(out var request, language: language));
        Assert.Equal(language, request.Language);
    }

    [Theory]
    [InlineData(null, "russian", null, null, 1, 48)]
    [InlineData(null, null, "dropped", null, 1, 48)]
    [InlineData(null, null, null, "name", 1, 48)]
    [InlineData(null, null, null, null, 0, 48)]
    [InlineData(null, null, null, null, 1, 0)]
    [InlineData(null, null, null, null, 1, 101)]
    public void RejectsInvalidValues(string? search, string? language, string? filter, string? sort, int page, int pageSize)
    {
        Assert.False(GamesCatalogRequestParser.TryParse(search, language, filter, sort, page, pageSize, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void RejectsTooLongSearch()
    {
        Assert.False(Parse(out _, search: new string('a', GamesCatalogRequestParser.MaxSearchLength + 1)));
    }

    [Fact]
    public async Task GamePageReturnsTheGame()
    {
        var game = new GamePageDto { Summary = new GameSummaryDto { TwitchId = "22197", Name = "DiRT 2" } };
        var bus = new GameBus(new GetGamePageResponse { Game = game });

        var result = await new GetGamePageQueryHandler(bus).Handle(new GetGamePageQuery("22197"), default);

        Assert.Same(game, result);
        Assert.Equal("22197", Assert.IsType<GetGamePageRequest>(bus.Sent).CategoryTwitchId);
    }

    [Fact]
    public async Task UnknownGameIsNotFound()
    {
        var bus = new GameBus(new GetGamePageResponse { Game = null });

        var error = await Assert.ThrowsAsync<AppException>(() =>
            new GetGamePageQueryHandler(bus).Handle(new GetGamePageQuery("404"), default));

        Assert.Equal(ErrorCodes.NotFound, error.ErrorCode);
    }

    [Theory]
    [InlineData("hollow-knight")]
    [InlineData("1; drop table")]
    [InlineData("")]
    public async Task NonNumericCategoryIsNotFoundWithoutAskingTheService(string twitchId)
    {
        var bus = new GameBus(new GetGamePageResponse());

        var error = await Assert.ThrowsAsync<AppException>(() =>
            new GetGamePageQueryHandler(bus).Handle(new GetGamePageQuery(twitchId), default));

        Assert.Equal(ErrorCodes.NotFound, error.ErrorCode);
        Assert.Null(bus.Sent);
    }

    [Fact]
    public async Task CatalogForwardsTheParsedRequest()
    {
        var catalog = new GetGamesCatalogResponse { TotalCount = 3 };
        var bus = new GameBus(catalog);
        var request = new GetGamesCatalogRequest { Language = "cs", Sort = GamesCatalogSort.Recent };

        var result = await new GetGamesCatalogQueryHandler(bus).Handle(new GetGamesCatalogQuery(request), default);

        Assert.Same(catalog, result);
        Assert.Same(request, bus.Sent);
    }

    private static bool Parse(
        out GetGamesCatalogRequest request,
        string? search = null,
        string? language = null,
        string? filter = null,
        string? sort = null,
        int page = 1,
        int pageSize = GamesCatalogRequestParser.DefaultPageSize) =>
        GamesCatalogRequestParser.TryParse(search, language, filter, sort, page, pageSize, out request, out _);

    internal sealed class GameBus(object response) : ITransportBus
    {
        public object? Sent { get; private set; }

        public Task<TransportResponse<TResponse>> SendRequestAsync<TRequest, TResponse>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : class where TResponse : class
        {
            Sent = request;
            return Task.FromResult(new TransportResponse<TResponse> { Success = (TResponse)response });
        }

        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class =>
            throw new NotSupportedException();

        public Task<EmptyResponse> SendRequestAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : class => throw new NotSupportedException();

        public Task<TransportResponse<TSuccess, TFailure>> SendRequestAsync<TRequest, TSuccess, TFailure>(
            TRequest request,
            CancellationToken cancellationToken = default)
            where TRequest : class where TSuccess : class where TFailure : class => throw new NotSupportedException();
    }
}
