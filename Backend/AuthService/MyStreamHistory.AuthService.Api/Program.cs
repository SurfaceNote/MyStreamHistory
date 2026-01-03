using System.Text;
using MyStreamHistory.AuthService.Api.Consumers;
using MyStreamHistory.AuthService.Api.Extensions;
using MyStreamHistory.AuthService.Infrastructure.Persistence;
using MyStreamHistory.Shared.Api.Extensions;
using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Logging;
using MyStreamHistory.Shared.Infrastructure.Persistence;
using MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork;
using MyStreamHistory.Shared.Infrastructure.Transport;

Console.OutputEncoding = Encoding.UTF8;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddDbContext<AuthDbContext>()
    .AddUnitOfWork<AuthDbContext>()
    .AddMassTransit(configureConsumers: configurator =>
    {
        configurator.AddConsumer<TwitchAuthorizeConsumer>();
    });

builder.Services
    .AddApplicationServices()
    .AddAppOptions(builder.Configuration);
    
var app = builder.Build();

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

app.ApplyDatabaseMigrations<AuthDbContext>();

app.Run();
