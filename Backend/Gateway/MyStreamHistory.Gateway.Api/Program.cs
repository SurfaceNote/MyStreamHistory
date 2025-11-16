using MyStreamHistory.Gateway.Api.Extenstions;
using MyStreamHistory.Shared.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration)
    .AddMassTransit();

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
