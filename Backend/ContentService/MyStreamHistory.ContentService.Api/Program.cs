using System.Text;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MyStreamHistory.ContentService.Api;
using MyStreamHistory.Shared.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.AddSentryObservability();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddCheck<ContentDatabaseHealthCheck>("content-database");

var connection = builder.Configuration.GetConnectionString("Content")
    ?? throw new InvalidOperationException("ConnectionStrings:Content is required.");
builder.Services.AddDbContext<ContentDbContext>(options => options.UseNpgsql(connection));

var storage = builder.Configuration.GetSection("ContentStorage").Get<ContentStorageOptions>()
    ?? throw new InvalidOperationException("ContentStorage configuration is required.");
storage.Validate(builder.Environment.IsProduction());
builder.Services.AddSingleton(storage);
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
    new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
    new AmazonS3Config { ServiceURL = storage.Endpoint, AuthenticationRegion = storage.Region, ForcePathStyle = true }));
builder.Services.AddScoped<MediaStorage>();
var delivery = builder.Configuration.GetSection("ContentDelivery").Get<ContentDeliveryOptions>() ?? new();
delivery.Validate();
builder.Services.AddSingleton(delivery);
builder.Services.AddSingleton<MediaDelivery>();
builder.Services.AddScoped<MediaDeletion>();
builder.Services.AddHostedService<MediaCleanupService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
var authorTwitchId = builder.Configuration["ContentAuthor:TwitchId"]
    ?? throw new InvalidOperationException("ContentAuthor:TwitchId is required.");
builder.Services.AddAuthorization(options => options.AddPolicy("ContentAuthor", policy =>
    policy.RequireAuthenticatedUser().RequireRole("admin")
        .RequireClaim("TwitchId", authorTwitchId)));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/ready");
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.Migrate();
app.Run();

namespace MyStreamHistory.ContentService.Api
{
    public sealed class ContentStorageOptions
    {
        public string Endpoint { get; init; } = "";
        public string Region { get; init; } = "";
        public string Bucket { get; init; } = "";
        public string AccessKey { get; init; } = "";
        public string SecretKey { get; init; } = "";

        public void Validate(bool production)
        {
            if (string.IsNullOrWhiteSpace(Region) || string.IsNullOrWhiteSpace(Bucket) || string.IsNullOrWhiteSpace(AccessKey)
                || string.IsNullOrWhiteSpace(SecretKey) || !Uri.TryCreate(Endpoint, UriKind.Absolute, out var url))
                throw new InvalidOperationException("Complete ContentStorage configuration is required.");
            if (production && (url.AbsoluteUri.TrimEnd('/') != "https://s3.twcstorage.ru"
                || Region != "ru-1"))
                throw new InvalidOperationException("Production content storage must use the Timeweb S3 endpoint and ru-1 region.");
            if (!production && url.Host.Equals("s3.twcstorage.ru", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Local content storage must not point to production Timeweb S3.");
        }
    }
}
