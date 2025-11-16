using MyStreamHistory.Shared.Infrastructure;
using MyStreamHistory.Shared.Infrastructure.Transport;

namespace MyStreamHistory.Gateway.Api.Extenstions
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
