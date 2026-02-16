using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MyStreamHistory.Gateway.Api.Extenstions;
using MyStreamHistory.Gateway.Application.Options;
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
    
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy
            .WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200",
                "http://mystreamhistory.com",
                "https://mystreamhistory.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("*")
        );
});

builder.Services.AddCors(o =>
{
    o.AddPolicy("ExtDev", p => p
        .WithOrigins("https://*.trycloudflare.com", "https://0fowmr0pl34smxhy55n0wj5fx5o9mn.ext-twitch.tv")
        .SetIsOriginAllowedToAllowWildcardSubdomains()
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddAutoMapperProfiles()
    .AddMediatRHandlers();

builder.Services.AddOptions<TwitchEventSubOptions>()
    .Bind(builder.Configuration.GetSection(TwitchEventSubOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
        
        // Handle authentication failures for refresh-token endpoint
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // For refresh-token endpoint, allow expired tokens
                if (context.Request.Path.StartsWithSegments("/auth/refresh-token"))
                {
                    // Only ignore lifetime validation errors
                    if (context.Exception is SecurityTokenExpiredException)
                    {
                        // Manually validate the expired token
                        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!));
                        
                        var validationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = false, // Ignore expiration
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = builder.Configuration["Jwt:Issuer"],
                            ValidAudience = builder.Configuration["Jwt:Audience"],
                            IssuerSigningKey = key
                        };
                        
                        try
                        {
                            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                            context.Principal = principal;
                            context.Success();
                        }
                        catch
                        {
                            // If validation fails for other reasons, keep the original failure
                        }
                    }
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AllowExpiredJwt", policy =>
    {
        policy.RequireAuthenticatedUser();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

// CORS должен быть ПЕРЕД Authentication/Authorization
// чтобы CORS заголовки добавлялись даже к 401 ответам
app.UseCors("Frontend");
app.UseCors("ExtDev");

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

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
