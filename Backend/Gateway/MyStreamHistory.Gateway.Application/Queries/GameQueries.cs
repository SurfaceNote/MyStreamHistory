using System.Text.RegularExpressions;
using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Games;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

public record GetGamePageQuery(string CategoryTwitchId) : IRequest<GamePageDto>;

public class GetGamePageQueryHandler(ITransportBus bus) : IRequestHandler<GetGamePageQuery, GamePageDto>
{
    // Twitch category ids are numeric; anything else cannot be a game we know.
    private static readonly Regex CategoryIdPattern = new("^[0-9]{1,20}$", RegexOptions.Compiled);

    public async Task<GamePageDto> Handle(GetGamePageQuery request, CancellationToken cancellationToken)
    {
        if (!CategoryIdPattern.IsMatch(request.CategoryTwitchId))
        {
            throw NotFound();
        }

        var response = await bus.SendRequestAsync<GetGamePageRequest, GetGamePageResponse>(
            new GetGamePageRequest { CategoryTwitchId = request.CategoryTwitchId },
            cancellationToken);

        return response.Success?.Game ?? throw NotFound();
    }

    private static AppException NotFound() => new(ErrorCodes.NotFound, "Game was not found");
}

public record GetGamesCatalogQuery(GetGamesCatalogRequest Request) : IRequest<GetGamesCatalogResponse>;

public class GetGamesCatalogQueryHandler(ITransportBus bus) : IRequestHandler<GetGamesCatalogQuery, GetGamesCatalogResponse>
{
    public async Task<GetGamesCatalogResponse> Handle(GetGamesCatalogQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<GetGamesCatalogRequest, GetGamesCatalogResponse>(
            request.Request,
            cancellationToken);

        return response.Success ?? throw new AppException(ErrorCodes.InternalError, "Games catalog is unavailable");
    }
}

/// <summary>Turns the query string of <c>GET /games</c> into a catalog request.</summary>
public static class GamesCatalogRequestParser
{
    public const int DefaultPageSize = 48;
    public const int MaxPageSize = 100;
    public const int MaxSearchLength = 100;

    // ISO 639-1 codes as Twitch reports them ("ru", "zh-hk") and Twitch's "other".
    private static readonly Regex LanguagePattern = new("^([a-z]{2,3}(-[a-z]{2,4})?|other)$", RegexOptions.Compiled);

    public static bool TryParse(
        string? search,
        string? language,
        string? filter,
        string? sort,
        int page,
        int pageSize,
        out GetGamesCatalogRequest request,
        out string? error)
    {
        request = new GetGamesCatalogRequest();
        error = null;

        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > MaxSearchLength)
        {
            error = $"search must be at most {MaxSearchLength} characters";
            return false;
        }

        language = string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant();
        if (language != null && !LanguagePattern.IsMatch(language))
        {
            error = "lang must be a language code such as \"ru\"";
            return false;
        }

        GamesCatalogFilter? parsedFilter = (filter?.Trim().ToLowerInvariant() ?? "") switch
        {
            "" or "all" => GamesCatalogFilter.All,
            "live" => GamesCatalogFilter.Live,
            "completed" => GamesCatalogFilter.Completed,
            _ => null
        };
        if (parsedFilter == null)
        {
            error = "filter must be all, live or completed";
            return false;
        }

        GamesCatalogSort? parsedSort = (sort?.Trim().ToLowerInvariant() ?? "") switch
        {
            "" or "popular" => GamesCatalogSort.Popular,
            "recent" => GamesCatalogSort.Recent,
            "hours" => GamesCatalogSort.Hours,
            _ => null
        };
        if (parsedSort == null)
        {
            error = "sort must be popular, recent or hours";
            return false;
        }

        if (page < 1 || pageSize is < 1 or > MaxPageSize)
        {
            error = $"page must be at least 1 and pageSize between 1 and {MaxPageSize}";
            return false;
        }

        request = new GetGamesCatalogRequest
        {
            Search = search,
            Language = language,
            Filter = parsedFilter.Value,
            Sort = parsedSort.Value,
            Page = page,
            PageSize = pageSize
        };
        return true;
    }
}
