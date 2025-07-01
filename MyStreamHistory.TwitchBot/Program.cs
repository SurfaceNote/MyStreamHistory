using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Sentry.AspNetCore;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using MyStreamHistory.TwitchBot.Services;

try
{
    var host = Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            services.AddHostedService<TwitchBotService>();
            services.AddSingleton<TwitchIrcService>();
            services.AddHttpClient("TwitchApi", client =>
            {
                client.BaseAddress = new Uri("https://api.twitch.tv/helix/");
                client.DefaultRequestHeaders.Add("Client-ID", "");
            });
        })
        .UseSerilog()
        .Build();
    await host.RunAsync();
}
catch
{

}