using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MyStreamHistory.Shared.Infrastructure
{
    public class InfrastructureBuilder(IServiceCollection services, IConfiguration configuration)
    {
        public IServiceCollection Services { get; } = services;
        public IConfiguration Configuration { get; } = configuration;
    }

}
