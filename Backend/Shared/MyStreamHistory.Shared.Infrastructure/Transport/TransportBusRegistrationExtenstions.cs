using Microsoft.Extensions.DependencyInjection;
using MyStreamHistory.Shared.Application.Transport;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public static class TransportBusRegistrationExtenstions
    {
        public static InfrastructureBuilder AddTransportBus(this InfrastructureBuilder builder)
        {
            builder.Services.AddScoped<ITransportBus, TransportBus>();
            return builder;
        }
    }
}
