using MyStreamHistory.Shared.Base.Contracts.Games;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Domain.Entities;
using Xunit;

namespace MyStreamHistory.TwitchTrackingService.Api.Tests;

public class GameConsumersTests
{
    private static readonly DateTime Now = new(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<GameSummary> Catalog =
    [
        Game("1", "Hollow Knight: Silksong", total: 63, recent: 20, channels: 4, ["ru", "en", "cs"], live: 1, completed: 2, lastDaysAgo: 0),
        Game("2", "Elden Ring Nightreign", total: 41, recent: 20, channels: 3, ["ru", "de"], lastDaysAgo: 1),
        Game("3", "Hades II", total: 29, recent: 8, channels: 2, ["ru"], live: 1, lastDaysAgo: 0.5),
        Game("4", "Kingdom Come: Deliverance II", total: 74, recent: 0, channels: 2, ["cs", "ru"], completed: 1, lastDaysAgo: 6),
        Game("5", "Ведьмак 3: Дикая Охота", total: 12, recent: 0, channels: 1, ["ru"], lastDaysAgo: 40)
    ];

    [Fact]
    public void PopularSortsByRecentHoursThenChannels()
    {
        var page = Read(new GetGamesCatalogRequest());

        Assert.Equal(new[] { "1", "2", "3", "4", "5" }, Ids(page));
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(5, page.CatalogCount);
    }

    [Fact]
    public void RecentAndHoursSorts()
    {
        Assert.Equal(new[] { "1", "3", "2", "4", "5" }, Ids(Read(new() { Sort = GamesCatalogSort.Recent })));
        Assert.Equal(new[] { "4", "1", "2", "3", "5" }, Ids(Read(new() { Sort = GamesCatalogSort.Hours })));
    }

    [Theory]
    [InlineData("hollow", "1")]
    [InlineData("  HADES ", "3")]
    [InlineData("ведьмак", "5")]
    public void SearchIgnoresCaseAndSurroundingSpaces(string search, string expectedId)
    {
        Assert.Equal(new[] { expectedId }, Ids(Read(new() { Search = search })));
    }

    [Fact]
    public void FiltersByLanguageLiveAndCompleted()
    {
        Assert.Equal(new[] { "1", "4" }, Ids(Read(new() { Language = "CS" })));
        Assert.Equal(new[] { "1", "3" }, Ids(Read(new() { Filter = GamesCatalogFilter.Live })));
        Assert.Equal(new[] { "1", "4" }, Ids(Read(new() { Filter = GamesCatalogFilter.Completed })));
        Assert.Equal(new[] { "4" }, Ids(Read(new() { Filter = GamesCatalogFilter.Completed, Language = "cs", Search = "kingdom" })));
    }

    [Fact]
    public void FiltersKeepTheWholeCatalogCountAndLanguages()
    {
        var page = Read(new() { Search = "no such game" });

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(5, page.CatalogCount);
        Assert.Equal(
            new[] { ("ru", 5), ("cs", 2), ("de", 1), ("en", 1) },
            page.Languages.Select(l => (l.Language, l.GamesCount)));
    }

    [Fact]
    public void PagesAreClampedAndDoNotOverlap()
    {
        var first = Read(new() { PageSize = 2 });
        var second = Read(new() { PageSize = 2, Page = 2 });
        var beyond = Read(new() { PageSize = 2, Page = 99 });
        var huge = Read(new() { PageSize = 1000, Page = 0 });

        Assert.Equal(new[] { "1", "2" }, Ids(first));
        Assert.Equal(new[] { "3", "4" }, Ids(second));
        Assert.Equal(3, beyond.Page);
        Assert.Equal(new[] { "5" }, Ids(beyond));
        Assert.Equal(GetGamesCatalogConsumer.MaxPageSize, huge.PageSize);
        Assert.Equal(1, huge.Page);
    }

    [Fact]
    public void EmptyCatalogHasOnePage()
    {
        var page = GetGamesCatalogConsumer.ReadPage([], new GetGamesCatalogRequest());

        Assert.Equal(1, page.Page);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void PageMapsStreamersWithRoundedHoursAndPlaythroughStatus()
    {
        var stats = new GameStats(
            Game("1", "Silksong", total: 12.345, recent: 1.25, channels: 1, ["ru"]),
            [
                new GameStreamerStats(7, "kotofeya", "Kotofeya", "avatar", "ru", 12.345, 10, Now.AddDays(-30), Now.AddDays(-2),
                    IsLiveWithGame: false, new GamePlaythroughSummary(PlaythroughStatus.Completed, "Silksong", 10))
            ]);

        var page = GameContractMapper.ToPage(stats)!;

        Assert.Equal(12.3, page.Summary.TotalHours);
        Assert.Equal(1.3, page.Summary.HoursLast30Days);
        Assert.True(page.Summary.IsGame);
        var streamer = Assert.Single(page.Streamers);
        Assert.Equal(12.3, streamer.Hours);
        Assert.Equal("Completed", streamer.Playthrough!.Status);
        Assert.Equal(10, streamer.Playthrough.StreamsCount);
        Assert.Null(GameContractMapper.ToPage(null));
    }

    private static GetGamesCatalogResponse Read(GetGamesCatalogRequest request) =>
        GetGamesCatalogConsumer.ReadPage(Catalog, request);

    [Fact]
    public void SitemapListsOnlyGamesWithEnoughStreamers()
    {
        var response = GetGameSitemapConsumer.Build(Catalog);

        Assert.Equal(["1", "2", "3", "4"], response.Games.Select(g => g.TwitchId));
        var game = response.Games.Single(g => g.TwitchId == "3");
        Assert.Equal("Hades II", game.Name);
        Assert.Equal(Now.AddDays(-0.5), game.LastStreamedAt);
    }

    [Fact]
    public void SitemapLeavesOutCategoriesThatAreNotGames()
    {
        var chatting = Game("509658", "Just Chatting", total: 500, recent: 50, channels: 10, ["ru"]) with { IgdbId = null };

        Assert.Empty(GetGameSitemapConsumer.Build([chatting]).Games);
    }

    private static string[] Ids(GetGamesCatalogResponse page) => page.Items.Select(g => g.TwitchId).ToArray();

    private static GameSummary Game(
        string id,
        string name,
        double total,
        double recent,
        int channels,
        string[] languages,
        int live = 0,
        int completed = 0,
        double lastDaysAgo = 1) =>
        new(Guid.NewGuid(), id, name, "art", "igdb-" + id, total, recent, channels * 3, channels, languages, live, completed,
            Now.AddDays(-100), Now.AddDays(-lastDaysAgo));
}
