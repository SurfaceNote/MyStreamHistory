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

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddDbContext<ViewerServiceDbContext>()
    .AddUnitOfWork<ViewerServiceDbContext>()
    .AddMassTransit(configureConsumers: configurator =>
    {
        configurator.AddConsumer<StreamCreatedConsumer>();
        configurator.AddConsumer<StreamEndedConsumer>();
        configurator.AddConsumer<ChatMessageConsumer>();
        configurator.AddConsumer<StreamCategoryChangedConsumer>();
        configurator.AddConsumer<GetStreamViewersConsumer>();
        configurator.AddConsumer<GetTopViewersConsumer>();
        configurator.AddConsumer<GetChatSubscriptionsConsumer>();
        configurator.AddConsumer<CleanupChatSubscriptionsConsumer>();
    })
    .AddTransportBus();

builder.Services
    .AddApplicationServices()
    .AddAppOptions(builder.Configuration);

// Add background services
builder.Services.AddHostedService<ViewerDataProcessingBackgroundService>();
builder.Services.AddHostedService<EventSubHistoryCleanupBackgroundService>();

var app = builder.Build();

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

app.ApplyDatabaseMigrations<ViewerServiceDbContext>();

app.Run();

