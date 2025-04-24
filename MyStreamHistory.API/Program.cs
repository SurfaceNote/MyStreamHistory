using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyStreamHistory.API.Data;
using MyStreamHistory.API.Extenstions;
using MyStreamHistory.API.Repositories;
using MyStreamHistory.API.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin",
        policy =>
        {
            policy.WithOrigins("http://localhost", "http://localhost:80", "http://localhost:4200", "http://mystreamhistory.com", "https://mystreamhistory.com")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

Env.Load();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
connectionString = connectionString?.Replace("%MY_DB_CONNECTION_STRING%", Environment.GetEnvironmentVariable("MY_DB_CONNECTION_STRING"));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ICompanyRepository, CompanyRepository>();

builder.Services.AddScoped<ITwitchAuthService, TwitchAuthService>();

builder.Services.AddControllers();

builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.WebHost.UseUrls("https://0.0.0.0:5000");

builder.Services.AddControllers().AddNewtonsoftJson();

var app = builder.Build();

app.UseCors("AllowSpecificOrigin");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
    app.ApplyMigrations();
}

if (args.Contains("--migrate"))
{
    app.ApplyMigrations();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
