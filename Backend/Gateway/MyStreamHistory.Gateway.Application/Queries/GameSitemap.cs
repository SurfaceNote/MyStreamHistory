using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using MediatR;
using MyStreamHistory.Shared.Application.Transport;
using MyStreamHistory.Shared.Base.Contracts.Games;
using MyStreamHistory.Shared.Base.Error;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Gateway.Application.Queries;

/// <summary>Sitemap of the indexable game pages, in every site language.</summary>
public record GetGameSitemapQuery : IRequest<string>;

public class GetGameSitemapQueryHandler(ITransportBus bus) : IRequestHandler<GetGameSitemapQuery, string>
{
    public async Task<string> Handle(GetGameSitemapQuery request, CancellationToken cancellationToken)
    {
        var response = await bus.SendRequestAsync<GetGameSitemapRequest, GetGameSitemapResponse>(
            new GetGameSitemapRequest(),
            cancellationToken);

        var games = response.Success?.Games ?? throw new AppException(ErrorCodes.InternalError, "Game sitemap is unavailable");
        return GameSitemap.Build(games);
    }
}

public static class GameSitemap
{
    public const string SiteOrigin = "https://mystreamhistory.com";

    /// <summary>The sitemap protocol allows 50,000 URLs per file.</summary>
    public const int MaxUrls = 50_000;

    /// <summary>Path prefixes of the site languages; English has none (see Frontend/src/app/i18n/languages.ts).</summary>
    public static readonly string[] LanguagePrefixes = ["", "/ru", "/cs", "/de", "/es"];

    private static readonly XNamespace Ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

    public static string Build(IEnumerable<GameSitemapEntryDto> games)
    {
        // When the limit is reached, the games streamed most recently stay in.
        var urls = games
            .OrderByDescending(g => g.LastStreamedAt)
            .ThenBy(g => g.TwitchId, StringComparer.Ordinal)
            .Take(MaxUrls / LanguagePrefixes.Length)
            .SelectMany(game => LanguagePrefixes.Select(prefix => new XElement(Ns + "url",
                new XElement(Ns + "loc", SiteOrigin + prefix + Path(game.TwitchId, game.Name)),
                new XElement(Ns + "lastmod", game.LastStreamedAt.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(Ns + "urlset", urls)).ToString();
    }

    /// <summary>Unlocalized game page path, the same one the frontend links to and treats as canonical.</summary>
    public static string Path(string twitchId, string name)
    {
        var slug = Slug(name);
        return "/game/" + (slug.Length > 0 ? twitchId + "-" + slug : twitchId);
    }

    private static readonly Dictionary<char, string> Cyrillic = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "yo", ['ж'] = "zh",
        ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o",
        ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts",
        ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "shch", ['ъ'] = "", ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu",
        ['я'] = "ya"
    };

    private static readonly Regex Combining = new("[̀-ͯ]", RegexOptions.Compiled);
    private static readonly Regex NotSlug = new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>
    /// Port of <c>articleSlug</c> (Frontend/src/app/features/content/article-slug.ts). The two must give
    /// the same result, or the sitemap would list URLs that the page then moves to another address;
    /// GameSitemapTests checks the cases the frontend tests check.
    /// </summary>
    public static string Slug(string title)
    {
        var latin = new StringBuilder(title.Length);
        foreach (var letter in title.ToLowerInvariant())
        {
            if (Cyrillic.TryGetValue(letter, out var replacement)) latin.Append(replacement);
            else if (letter == 'ß') latin.Append("ss");
            // JavaScript lowercases İ to i + combining dot (Unicode SpecialCasing); .NET keeps it.
            else if (letter == 'İ') latin.Append('i');
            else latin.Append(letter);
        }

        var ascii = Combining.Replace(latin.ToString().Normalize(NormalizationForm.FormKD), "");
        var slug = NotSlug.Replace(ascii, "-");
        if (slug.Length > 90) slug = slug[..90];
        if (slug.StartsWith('-')) slug = slug[1..];
        if (slug.EndsWith('-')) slug = slug[..^1];
        return slug;
    }
}
