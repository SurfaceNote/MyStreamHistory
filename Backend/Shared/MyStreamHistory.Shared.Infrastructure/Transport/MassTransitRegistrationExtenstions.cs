using MassTransit;
using MyStreamHistory.Shared.Infrastructure.Transport;
using MyStreamHistory.Shared.Infrastructure.Configuration;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public static class MassTransitRegistrationExtenstions
    {
        public static InfrastructureBuilder AddMassTransit(
            this InfrastructureBuilder builder,
            Action<IBusRegistrationConfigurator>? configureConsumers = null,
            Action<IRabbitMqBusFactoryConfigurator, IBusRegistrationContext>? configureTransport = null)
        {
            var rabbitOptions = builder.Configuration.GetSection("RabbitMq").GetValidated<RabbitMqOptions>();
            var massTransitOptions = builder.Configuration.GetSection("MassTransit").GetValidated<MassTransitOptions>();

            builder.Services.AddMassTransit(x =>
            {
                configureConsumers?.Invoke(x);

                x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(massTransitOptions.EndpointPrefix, false));

                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitOptions.Host, rabbitOptions.VirtualHost, h =>
                    {
                        h.Username(rabbitOptions.Username);
                        h.Password(rabbitOptions.Password);
                    });

                    if (massTransitOptions.UseInMemoryOutbox)
                    {
                        cfg.UseInMemoryOutbox(context);
                    }

                    if (massTransitOptions.UseRetry)
                    {
                        cfg.UseMessageRetry(r => r.Intervals(massTransitOptions.RetryIntervals));
                    }

                    configureTransport?.Invoke(cfg, context);

                    cfg.ConfigureEndpoints(context);
                });
            });

            return builder;
        }
    }
}
