using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Application.UnitOfWork;

namespace MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork
{
    public static class UnitOfworkRegistrationExtensions
    {
        public static InfrastructureBuilder AddUnitOfWork<TDbContext>(this InfrastructureBuilder builder)
            where TDbContext : DbContext
        {
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork<TDbContext>>();
            return builder;
        }
    }
}
