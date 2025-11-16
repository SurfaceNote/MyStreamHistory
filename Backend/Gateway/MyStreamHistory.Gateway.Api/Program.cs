using MyStreamHistory.Gateway.Api.Extenstions;
using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddMassTransit();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
