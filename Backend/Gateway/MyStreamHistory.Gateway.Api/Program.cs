using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;
using MyStreamHistory.Gateway.Api.Extenstions;
using MyStreamHistory.Shared.Api.Extensions;
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
        );
});

builder.Services.AddAutoMapperProfiles()
    .AddMediatRHandlers();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};

forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseAuthentication();
app.UseAuthorization();

app.UseRouting();
app.UseCors("Frontend");

app.UseGlobalExceptionHandler();
app.UseAppExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHsts();

app.MapControllers();

app.Run();
