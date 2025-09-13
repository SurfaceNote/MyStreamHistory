using System.Net.Http.Headers;
using DotNetEnv;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

Env.Load();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHttpClient("TwitchApi", c =>
{
   c.BaseAddress = new Uri("https://api.twitch.tv/helix/");
   c.DefaultRequestHeaders.Add("Client-ID", "");
   c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "");
});

builder.Services.AddHttpClient("Backend", c =>
{
   c.BaseAddress = new Uri("");
});

await builder.Build().RunAsync();