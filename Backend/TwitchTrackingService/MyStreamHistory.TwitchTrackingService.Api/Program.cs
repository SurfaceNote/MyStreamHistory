using MyStreamHistory.Shared.Api.Features;
using MassTransit;
using System.Text;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Logging;
using MyStreamHistory.Shared.Infrastructure.Persistence;
using MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork;
using MyStreamHistory.Shared.Infrastructure.Transport;
using MyStreamHistory.TwitchTrackingService.Api.BackgroundServices;
using MyStreamHistory.TwitchTrackingService.Api.Consumers;
using MyStreamHistory.TwitchTrackingService.Infrastructure;
using MyStreamHistory.TwitchTrackingService.Infrastructure.Persistence;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);
builder.AddSentryObservability();
var twitchDataCollection = builder.AddTwitchDataCollectionFeature();

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddDbContext<TwitchTrackingDbContext>()
    .AddUnitOfWork<TwitchTrackingDbContext>()
    .AddMassTransit(configureConsumers: configurator =>
    {
        configurator.AddEntityFrameworkOutbox<TwitchTrackingDbContext>(outbox =>
        {
            outbox.UsePostgres();
            outbox.UseBusOutbox();
            outbox.QueryDelay = TimeSpan.FromSeconds(1);
        });

        configurator.AddConsumer<StreamOnlineConsumer>();
        configurator.AddConsumer<StreamOfflineConsumer>();
        configurator.AddConsumer<ChannelUpdateConsumer>();
        configurator.AddConsumer<UserRegisteredConsumer>();
        configurator.AddConsumer<GetRecentStreamsConsumer>();
        configurator.AddConsumer<GetLiveStreamersConsumer>();
        configurator.AddConsumer<GetStreamSessionByIdConsumer>();
        configurator.AddConsumer<GetEventSubSubscriptionsConsumer>();
        configurator.AddConsumer<DeleteAllSubscriptionsConsumer>();
        configurator.AddConsumer<SubscribeToAllUsersConsumer>();
        configurator.AddConsumer<GetActiveStreamCategoryConsumer>();
        configurator.AddConsumer<GetStreamerStatisticsConsumer>();
        configurator.AddConsumer<GetViewerWatchHistoryConsumer>();
        configurator.AddConsumer<GetPlaythroughSettingsConsumer>();
        configurator.AddConsumer<UpsertPlaythroughConsumer>();
        configurator.AddConsumer<DeletePlaythroughConsumer>();
    })
    .AddTransportBus();

builder.Services.AddTwitchTrackingInfrastructure(builder.Configuration);

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);

if (twitchDataCollection.Enabled)
{
    builder.Services.AddHostedService<SubscriptionSyncBackgroundService>();
    builder.Services.AddHostedService<StreamDataPollingBackgroundService>();
}

var app = builder.Build();
app.Logger.LogInformation("Twitch data collection enabled: {Enabled}", twitchDataCollection.Enabled);

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

app.ApplyDatabaseMigrations<TwitchTrackingDbContext>();

app.Run();
