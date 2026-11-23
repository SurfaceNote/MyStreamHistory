using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyStreamHistory.Gateway.Application.Queries;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Api.Wrappers;
using MyStreamHistory.Shared.Base.Contracts.Games;

namespace MyStreamHistory.Gateway.Api.Controllers;

/// <summary>Public game pages and the games catalog.</summary>
[ApiController]
[AllowAnonymous]
[Route("games")]
public class GamesController(IMediator mediator) : ApiControllerBase
{
    // Short enough that the "live now" badges stay close to reality.
    private const string PublicCache = "public, max-age=60";

    // Crawlers read the sitemap rarely; an hour spares the full catalog calculation.
    private const string SitemapCache = "public, max-age=3600";

    /// <summary>Games streamed on the site, with search, filters, sorting and paging.</summary>
    /// <param name="search">Part of the game name, case-insensitive.</param>
    /// <param name="lang">Language code: games with a streamer whose main language it is.</param>
    /// <param name="filter">all (default), live or completed.</param>
    /// <param name="sort">popular (default, hours in the last 30 days), recent or hours.</param>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResultContainer<GetGamesCatalogResponse>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 400)]
    public async Task<ActionResult<ApiResultContainer<GetGamesCatalogResponse>>> GetCatalog(
        [FromQuery] string? search,
        [FromQuery] string? lang,
        [FromQuery] string? filter,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = GamesCatalogRequestParser.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!GamesCatalogRequestParser.TryParse(search, lang, filter, sort, page, pageSize, out var request, out var error))
        {
            return BadRequest(new ApiResultContainer { Success = false, Errors = [error!] });
        }

        var catalog = await mediator.Send(new GetGamesCatalogQuery(request), cancellationToken);
        Response.Headers.CacheControl = PublicCache;
        return this.Success(catalog);
    }

    /// <summary>
    /// Sitemap of the indexable game pages in every site language. The site serves it as
    /// /sitemap-games.xml (Frontend/nginx/default.conf), and robots.txt points to it.
    /// </summary>
    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> GetSitemap(CancellationToken cancellationToken = default)
    {
        var xml = await mediator.Send(new GetGameSitemapQuery(), cancellationToken);
        Response.Headers.CacheControl = SitemapCache;
        return Content(xml, "application/xml");
    }

    /// <summary>Statistics of one Twitch category: who streamed it, in which language, and their playthroughs.</summary>
    [HttpGet("{twitchId}")]
    [ProducesResponseType(typeof(ApiResultContainer<GamePageDto>), 200)]
    [ProducesResponseType(typeof(ApiResultContainer), 404)]
    public async Task<ActionResult<ApiResultContainer<GamePageDto>>> GetGame(
        [FromRoute] string twitchId,
        CancellationToken cancellationToken = default)
    {
        var game = await mediator.Send(new GetGamePageQuery(twitchId), cancellationToken);
        Response.Headers.CacheControl = PublicCache;
        return this.Success(game);
    }
}
