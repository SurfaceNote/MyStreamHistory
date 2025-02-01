using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using MyStreamHistory.API.Data;
using MyStreamHistory.API.Repositories;

var builder = WebApplication.CreateBuilder(args);

Env.Load();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
connectionString = connectionString?.Replace("%MY_DB_CONNECTION_STRING%", Environment.GetEnvironmentVariable("MY_DB_CONNECTION_STRING"));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IStreamerRepository, StreamerRepository>();


builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();