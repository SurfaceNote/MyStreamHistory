using DotNetEnv;
using MyStreamHistory.TwitchBot.Middleware;
using MyStreamHistory.TwitchBot.Services;
using MyStreamHistory.TwitchBot.Workers;
using Serilog;
using Serilog.Events;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddSerilog(config =>
{
   config.MinimumLevel.Override("Microsoft", LogEventLevel.Information)
         .Enrich.FromLogContext()
         .Enrich.WithProperty("Application", "TwitchBot");
   
   config.WriteTo.Console();
});

builder.Services.AddTransient<TwitchAuthHandler>();
builder.Services.AddHttpClient("TwitchApi", c =>
{
   c.BaseAddress = new Uri("https://api.twitch.tv/helix/");
}).AddHttpMessageHandler<TwitchAuthHandler>();

builder.Services.AddHttpClient("Backend", c =>
{
   c.BaseAddress = new Uri("");
});

builder.Services.AddControllers()
   .AddJsonOptions(options =>
   {
      options.JsonSerializerOptions.PropertyNamingPolicy = null;
   });

builder.Services.AddSingleton<TwitchAuthService>();
builder.Services.AddSingleton<ITwitchEventDispatcher, TwitchEventDispatcher>();
builder.Services.AddHostedService<EventSubWorker>();

var app = builder.Build();

app.UseMiddleware<TwitchEventSubMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();

await app.RunAsync();