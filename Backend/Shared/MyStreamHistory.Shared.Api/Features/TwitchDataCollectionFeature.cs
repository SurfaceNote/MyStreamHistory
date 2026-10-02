using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MyStreamHistory.Shared.Api.Features;

public sealed record TwitchDataCollectionFeature(bool Enabled)
{
    public const string ConfigurationKey = "Features:TwitchDataCollection:Enabled";

    public static TwitchDataCollectionFeature FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
        => new(configuration.GetValue<bool?>(ConfigurationKey) ?? !environment.IsDevelopment());

    public void EnsureEnabled()
    {
        if (!Enabled)
            throw new InvalidOperationException("Twitch data collection is disabled by Features:TwitchDataCollection:Enabled.");
    }
}

public static class TwitchDataCollectionRegistration
{
    public static TwitchDataCollectionFeature AddTwitchDataCollectionFeature(this WebApplicationBuilder builder)
    {
        var feature = TwitchDataCollectionFeature.FromConfiguration(builder.Configuration, builder.Environment);
        builder.Services.AddSingleton(feature);
        builder.Services.AddScoped<TwitchDataCollectionFilter>();
        return feature;
    }
}

// Block EventSub and subscription diagnostics before they can publish messages or call Twitch.
public sealed class TwitchDataCollectionFilter(TwitchDataCollectionFeature feature) : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!feature.Enabled)
            context.Result = new ObjectResult("Twitch data collection is disabled.")
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
