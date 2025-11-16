using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Application.Repository;

namespace MyStreamHistory.Shared.Infrastructure.Persistence.Repository
{
    public static class RepositoryRegistrationExtenstions
    {
        public static InfrastructureBuilder AddRepository<TEntity, TDbContext>(this InfrastructureBuilder builder)
            where TDbContext : DbContext
            where TEntity : class
        {
            builder.Services.AddScoped<IRepositoryBase<TEntity>, RepositoryBase<TEntity, TDbContext>>();
            return builder;
        }
    }
}
