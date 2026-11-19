using MassTransit;
using MyStreamHistory.Shared.Infrastructure.Transport;
using MyStreamHistory.Shared.Infrastructure.Configuration;
using MyStreamHistory.Shared.Base.Exceptions;

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

                    cfg.PropagateTraceContext();
                    cfg.RespondToAppExceptions();

                    if (massTransitOptions.UseInMemoryOutbox)
                    {
                        cfg.UseInMemoryOutbox(context);
                    }

                    if (massTransitOptions.UseRetry)
                    {
                        cfg.UseRetryExceptBusinessErrors(massTransitOptions.RetryIntervals);
                    }

                    configureTransport?.Invoke(cfg, context);

                    cfg.ConfigureEndpoints(context);
                });
            });

            return builder;
        }

        public static void UseRetryExceptBusinessErrors(this IBusFactoryConfigurator configurator, params int[] intervals)
        {
            configurator.UseMessageRetry(r =>
            {
                r.Intervals(intervals);
                // A business error gives the same answer on every attempt, and retrying can change it:
                // RefreshTokenConsumer revokes an expired token before throwing, so a retry would report
                // invalid_token instead of token_expired.
                r.Ignore<AppException>();
            });
        }
    }
}
