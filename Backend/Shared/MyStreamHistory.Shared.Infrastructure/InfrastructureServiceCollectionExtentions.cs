using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamHistory.Shared.Infrastructure
{
    public static class InfrastructureServiceCollectionExtentions
    {
        public static InfrastructureBuilder AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            return new InfrastructureBuilder(services, configuration);
        }
    }
}
