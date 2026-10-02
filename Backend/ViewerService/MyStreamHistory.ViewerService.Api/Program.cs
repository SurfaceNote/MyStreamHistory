using MyStreamHistory.Shared.Api.Features;
using System.Text;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Logging;
using MyStreamHistory.Shared.Infrastructure.Persistence;
using MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork;
using MyStreamHistory.Shared.Infrastructure.Transport;
using MyStreamHistory.ViewerService.Api.BackgroundServices;
using MyStreamHistory.ViewerService.Api.Consumers;
using MyStreamHistory.ViewerService.Api.Extensions;
using MyStreamHistory.ViewerService.Infrastructure.Persistence;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);
builder.AddSentryObservability();
var twitchDataCollection = builder.AddTwitchDataCollectionFeature();

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddDbContext<ViewerServiceDbContext>()
    .AddUnitOfWork<ViewerServiceDbContext>()
    .AddMassTransit(configureConsumers: configurator =>
    {
        configurator.AddConsumer<StreamCreatedConsumer>();
        configurator.AddConsumer<StreamEndedConsumer>();
        configurator.AddConsumer<StreamLiveConfirmationLostConsumer>();
        configurator.AddConsumer<StreamLiveConfirmationRestoredConsumer>();
        configurator.AddConsumer<ChatMessageConsumer>();
        configurator.AddConsumer<StreamCategoryChangedConsumer>();
        configurator.AddConsumer<GetStreamViewersConsumer>();
        configurator.AddConsumer<GetUniqueViewerCountsConsumer>();
        configurator.AddConsumer<GetTopViewersConsumer>();
        configurator.AddConsumer<GetStreamerViewerStatsConsumer>();
        configurator.AddConsumer<GetChatSubscriptionsConsumer>();
        configurator.AddConsumer<CleanupChatSubscriptionsConsumer>();
    })
    .AddTransportBus();

builder.Services
    .AddApplicationServices()
    .AddAppOptions(builder.Configuration);

if (twitchDataCollection.Enabled)
{
    builder.Services.AddHostedService<ViewerDataProcessingBackgroundService>();
    builder.Services.AddHostedService<EventSubHistoryCleanupBackgroundService>();
}

var app = builder.Build();
app.Logger.LogInformation("Twitch data collection enabled: {Enabled}", twitchDataCollection.Enabled);

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

app.ApplyDatabaseMigrations<ViewerServiceDbContext>();

app.Run();

