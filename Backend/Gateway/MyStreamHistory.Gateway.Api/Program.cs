using Microsoft.OpenApi;
using MyStreamHistory.Gateway.Api.Extenstions;
using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Logging;
using MyStreamHistory.Shared.Infrastructure.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration)
    .AddSerilog()
    .AddMassTransit()
    .AddTransportBus();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Load JWT Token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
