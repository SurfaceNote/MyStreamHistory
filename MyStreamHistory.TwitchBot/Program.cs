using System.Net.Http.Headers;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyStreamHistory.TwitchBot.Services;
using Serilog;
using Serilog.Events;

Env.Load();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSerilog(config =>
{
   config.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
         .Enrich.FromLogContext()
         .Enrich.WithProperty("Application", "TwitchBot");
   
   config.WriteTo.Console();
});


builder.Services.AddHttpClient("TwitchApi", c =>
{
   c.BaseAddress = new Uri("https://api.twitch.tv/helix/");
   c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");
});

builder.Services.AddHttpClient("Backend", c =>
{
   c.BaseAddress = new Uri("");
});

builder.Services.AddSingleton<TwitchAuthService>();

await builder.Build().RunAsync();