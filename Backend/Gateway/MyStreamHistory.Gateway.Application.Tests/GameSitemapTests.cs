using System.Xml.Linq;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Base.Contracts.Games;
using Xunit;

namespace MyStreamHistory.Gateway.Application.Tests;

public class GameSitemapTests
{
    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    // Expected values come from the frontend articleSlug (article-slug.ts), run with Node:
    // both sides must build one URL for a game.
    [Theory]
    [InlineData("English article", "english-article")]
    [InlineData("Русская статья", "russkaya-statya")]
    [InlineData("Český článek", "cesky-clanek")]
    [InlineData("Größter Überblick", "grosster-uberblick")]
    [InlineData("Reseña española", "resena-espanola")]
    [InlineData("colin mcrae: DiRT 2", "colin-mcrae-dirt-2")]
    [InlineData("Ведьмак 3: Дикая Охота", "vedmak-3-dikaya-okhota")]
    [InlineData("Ёлки: съешь же ещё", "yolki-sesh-zhe-eshchyo")]
    [InlineData("ﬁnal ﬀ İstanbul ẞ", "final-ff-istanbul-ss")]
    [InlineData("  --Hollow   Knight!!  ", "hollow-knight")]
    [InlineData("！！", "")]
    public void SlugMatchesTheFrontend(string name, string slug)
    {
        Assert.Equal(slug, GameSitemap.Slug(name));
    }

    [Fact]
    public void LongNamesAreCutToNinetyCharactersWithoutATrailingDash()
    {
        var slug = GameSitemap.Slug(new string('a', 89) + " b");

        Assert.Equal(new string('a', 89), slug);
    }

    [Fact]
    public void PathFallsBackToTheIdWhenTheNameHasNothingToSlug()
    {
        Assert.Equal("/game/22197-colin-mcrae-dirt-2", GameSitemap.Path("22197", "colin mcrae: DiRT 2"));
        Assert.Equal("/game/7", GameSitemap.Path("7", "！！"));
    }

    [Fact]
    public void ListsEveryGameInEveryLanguageWithItsLastStreamDate()
    {
        var xml = XDocument.Parse(GameSitemap.Build([
            new GameSitemapEntryDto { TwitchId = "22197", Name = "colin mcrae: DiRT 2", LastStreamedAt = new DateTime(2026, 2, 20, 23, 30, 0, DateTimeKind.Utc) },
            new GameSitemapEntryDto { TwitchId = "1", Name = "Tom & Jerry", LastStreamedAt = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc) }
        ]));

        var urls = xml.Root!.Elements(Ns + "url").Select(url => (url.Element(Ns + "loc")!.Value, url.Element(Ns + "lastmod")!.Value)).ToList();
        Assert.Equal(10, urls.Count);
        Assert.Contains(("https://mystreamhistory.com/game/22197-colin-mcrae-dirt-2", "2026-02-20"), urls);
        Assert.Contains(("https://mystreamhistory.com/ru/game/22197-colin-mcrae-dirt-2", "2026-02-20"), urls);
        Assert.Contains(("https://mystreamhistory.com/es/game/1-tom-jerry", "2026-11-01"), urls);
        // Most recently streamed first.
        Assert.Equal("https://mystreamhistory.com/game/1-tom-jerry", urls[0].Item1);
    }

    [Fact]
    public void StaysWithinTheSitemapUrlLimit()
    {
        var games = Enumerable.Range(1, 10_001).Select(i => new GameSitemapEntryDto
        {
            TwitchId = i.ToString(), Name = "Game " + i, LastStreamedAt = new DateTime(2026, 1, 1).AddMinutes(i)
        });

        var xml = XDocument.Parse(GameSitemap.Build(games));

        Assert.Equal(GameSitemap.MaxUrls, xml.Root!.Elements(Ns + "url").Count());
        Assert.DoesNotContain("/game/1-game-1<", xml.ToString());
    }

    [Fact]
    public async Task HandlerAsksForTheIndexableGames()
    {
        var bus = new GameQueriesTests.GameBus(new GetGameSitemapResponse
        {
            Games = [new GameSitemapEntryDto { TwitchId = "22197", Name = "colin mcrae: DiRT 2", LastStreamedAt = DateTime.UtcNow }]
        });

        var xml = await new GetGameSitemapQueryHandler(bus).Handle(new GetGameSitemapQuery(), CancellationToken.None);

        Assert.IsType<GetGameSitemapRequest>(bus.Sent);
        Assert.Contains("/cs/game/22197-colin-mcrae-dirt-2", xml);
    }
}
