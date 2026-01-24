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

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddDbContext<TwitchTrackingDbContext>()
    .AddUnitOfWork<TwitchTrackingDbContext>()
    .AddMassTransit(configureConsumers: configurator =>
    {
        configurator.AddConsumer<StreamOnlineConsumer>();
        configurator.AddConsumer<StreamOfflineConsumer>();
        configurator.AddConsumer<UserRegisteredConsumer>();
        configurator.AddConsumer<GetRecentStreamsConsumer>();
    })
    .AddTransportBus();

builder.Services.AddTwitchTrackingInfrastructure(builder.Configuration);

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Program).Assembly);

// Add Background Services
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<SubscriptionSyncBackgroundService>();
}

// Add Stream Data Polling Background Service (runs in all environments)
builder.Services.AddHostedService<StreamDataPollingBackgroundService>();

var app = builder.Build();

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

app.ApplyDatabaseMigrations<TwitchTrackingDbContext>();

app.Run();
