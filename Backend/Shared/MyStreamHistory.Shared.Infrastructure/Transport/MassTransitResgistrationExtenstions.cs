using MyStreamHistory.Shared.Infrastructure.Transport;
using MyStreamHistory.Shared.Infrastructure;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public static class MassTransitRegistrationExtensions
    {
        public static InfrastructureBuilder AddMassTransit(this InfrastructureBuilder builder)
        {
            builder.AddMassTransit(configureConsumers: cfg => { },
                configureTransport: (cfg, context) => { });
            return builder;
        }
    }

}
