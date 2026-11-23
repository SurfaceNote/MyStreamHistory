using MassTransit;
using MyStreamHistory.Shared.Base.Contracts.Games;
using MyStreamHistory.TwitchTrackingService.Application.DTOs;
using MyStreamHistory.TwitchTrackingService.Application.Services;

namespace MyStreamHistory.TwitchTrackingService.Api.Consumers;

public class GetGamePageConsumer(IGameStatsService gameStats) : IConsumer<GetGamePageRequest>
{
    public async Task Consume(ConsumeContext<GetGamePageRequest> context)
    {
        var categoryTwitchId = context.Message.CategoryTwitchId.Trim();
        var game = categoryTwitchId.Length == 0
            ? null
            : await gameStats.GetGameAsync(categoryTwitchId, context.CancellationToken);
        await context.RespondAsync(new GetGamePageResponse { Game = GameContractMapper.ToPage(game) });
    }
}

public class GetGamesCatalogConsumer(IGameStatsService gameStats) : IConsumer<GetGamesCatalogRequest>
{
    public const int MaxPageSize = 100;

    public async Task Consume(ConsumeContext<GetGamesCatalogRequest> context)
    {
        var catalog = await gameStats.GetCatalogAsync(context.CancellationToken);
        await context.RespondAsync(ReadPage(catalog, context.Message));
    }

    public static GetGamesCatalogResponse ReadPage(IReadOnlyList<GameSummary> catalog, GetGamesCatalogRequest request)
    {
        var search = request.Search?.Trim();
        var language = request.Language?.Trim().ToLowerInvariant();
        IEnumerable<GameSummary> games = catalog;

        if (!string.IsNullOrEmpty(search))
        {
            games = games.Where(g => g.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(language))
        {
            games = games.Where(g => g.Languages.Contains(language));
        }

        games = request.Filter switch
        {
            GamesCatalogFilter.Live => games.Where(g => g.LiveCount > 0),
            GamesCatalogFilter.Completed => games.Where(g => g.CompletedCount > 0),
            _ => games
        };

        var sorted = Sort(games, request.Sort).ToList();
        var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);
        var lastPage = Math.Max(1, (int)Math.Ceiling(sorted.Count / (double)pageSize));
        var page = Math.Clamp(request.Page, 1, lastPage);

        return new GetGamesCatalogResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = sorted.Count,
            CatalogCount = catalog.Count,
            Languages = catalog
                .SelectMany(g => g.Languages)
                .GroupBy(l => l)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new GameLanguageCountDto { Language = g.Key, GamesCount = g.Count() })
                .ToList(),
            Items = sorted
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(GameContractMapper.ToSummary)
                .ToList()
        };
    }

    // Name and Twitch id break ties so that pages never overlap.
    private static IEnumerable<GameSummary> Sort(IEnumerable<GameSummary> games, GamesCatalogSort sort) =>
        (sort switch
        {
            GamesCatalogSort.Recent => games.OrderByDescending(g => g.LastStreamedAt),
            GamesCatalogSort.Hours => games.OrderByDescending(g => g.TotalHours),
            _ => games
                .OrderByDescending(g => g.HoursLast30Days)
                .ThenByDescending(g => g.ChannelsCount)
                .ThenByDescending(g => g.TotalHours)
        })
        .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(g => g.TwitchId, StringComparer.Ordinal);
}

public static class GameContractMapper
{
    public static GamePageDto? ToPage(GameStats? game) => game == null
        ? null
        : new GamePageDto
        {
            Summary = ToSummary(game.Summary),
            Streamers = game.Streamers.Select(ToStreamer).ToList()
        };

    public static GameSummaryDto ToSummary(GameSummary summary) => new()
    {
        TwitchId = summary.TwitchId,
        Name = summary.Name,
        BoxArtUrl = summary.BoxArtUrl,
        IgdbId = summary.IgdbId,
        IsGame = summary.IsGame,
        TotalHours = RoundHours(summary.TotalHours),
        HoursLast30Days = RoundHours(summary.HoursLast30Days),
        StreamsCount = summary.StreamsCount,
        ChannelsCount = summary.ChannelsCount,
        Languages = summary.Languages.ToList(),
        LiveCount = summary.LiveCount,
        CompletedCount = summary.CompletedCount,
        FirstStreamedAt = summary.FirstStreamedAt,
        LastStreamedAt = summary.LastStreamedAt
    };

    private static GameStreamerDto ToStreamer(GameStreamerStats streamer) => new()
    {
        TwitchUserId = streamer.TwitchUserId,
        StreamerLogin = streamer.StreamerLogin,
        StreamerDisplayName = streamer.StreamerDisplayName,
        StreamerAvatarUrl = streamer.StreamerAvatarUrl,
        Language = streamer.Language,
        Hours = RoundHours(streamer.Hours),
        StreamsCount = streamer.StreamsCount,
        FirstStreamedAt = streamer.FirstStreamedAt,
        LastStreamedAt = streamer.LastStreamedAt,
        IsLiveWithGame = streamer.IsLiveWithGame,
        Playthrough = streamer.Playthrough == null
            ? null
            : new GamePlaythroughDto
            {
                Status = streamer.Playthrough.Status.ToString(),
                Title = streamer.Playthrough.Title,
                StreamsCount = streamer.Playthrough.StreamsCount
            }
    };

    private static double RoundHours(double hours) => Math.Round(hours, 1, MidpointRounding.AwayFromZero);
}

public class GetGameSitemapConsumer(IGameStatsService gameStats) : IConsumer<GetGameSitemapRequest>
{
    /// <summary>
    /// A game page with fewer streamers is too thin to index; the frontend marks it noindex
    /// by the same rule (MIN_INDEXED_STREAMERS in game-page.component.ts).
    /// </summary>
    public const int MinIndexedStreamers = 2;

    public async Task Consume(ConsumeContext<GetGameSitemapRequest> context)
    {
        var catalog = await gameStats.GetCatalogAsync(context.CancellationToken);
        await context.RespondAsync(Build(catalog));
    }

    /// <summary>Indexable games only: real games (the catalog holds nothing else) with enough streamers.</summary>
    public static GetGameSitemapResponse Build(IReadOnlyList<GameSummary> catalog) => new()
    {
        Games = catalog
            .Where(g => g.IsGame && g.ChannelsCount >= MinIndexedStreamers)
            .OrderBy(g => g.TwitchId, StringComparer.Ordinal)
            .Select(g => new GameSitemapEntryDto { TwitchId = g.TwitchId, Name = g.Name, LastStreamedAt = g.LastStreamedAt })
            .ToList()
    };
}
