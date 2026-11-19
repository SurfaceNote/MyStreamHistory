using MassTransit;
using MyStreamHistory.Shared.Base.Contracts;
using MyStreamHistory.Shared.Base.Exceptions;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public static class AppExceptionResponses
    {
        /// <summary>
        /// Answers a request with <see cref="BaseFailedResponseContract"/> when its consumer throws
        /// <see cref="AppException"/> and the caller accepts that response type.
        /// </summary>
        /// <remarks>
        /// An <see cref="AppException"/> is an expected business outcome (expired token, missing user), not a
        /// failure. Without this filter MassTransit turns it into a <c>Fault</c>: the caller gets
        /// <c>RequestFaultException</c> (HTTP 500 in Gateway) and the message lands in the error queue.
        /// The filter wraps each consumer directly, so the exception is handled before MassTransit reports
        /// the fault. Callers that do not accept the failure contract still get the fault, which
        /// <see cref="TransportBus"/> turns back into <see cref="AppException"/>.
        /// </remarks>
        public static void RespondToAppExceptions(this IBusFactoryConfigurator configurator)
        {
            configurator.ConnectConsumerConfigurationObserver(new ConsumerObserver());
        }

        private sealed class ConsumerObserver : IConsumerConfigurationObserver
        {
            public void ConsumerConfigured<TConsumer>(IConsumerConfigurator<TConsumer> configurator)
                where TConsumer : class
            {
            }

            public void ConsumerMessageConfigured<TConsumer, TMessage>(IConsumerMessageConfigurator<TConsumer, TMessage> configurator)
                where TConsumer : class
                where TMessage : class
            {
                configurator.UseFilter(new RespondFilter<TConsumer, TMessage>());
            }
        }

        private sealed class RespondFilter<TConsumer, TMessage> : IFilter<ConsumerConsumeContext<TConsumer, TMessage>>
            where TConsumer : class
            where TMessage : class
        {
            public async Task Send(ConsumerConsumeContext<TConsumer, TMessage> context, IPipe<ConsumerConsumeContext<TConsumer, TMessage>> next)
            {
                try
                {
                    await next.Send(context);
                }
                catch (AppException exception) when (context.RequestId.HasValue
                    && context.IsResponseAccepted<BaseFailedResponseContract>(defaultIfHeaderNotFound: false))
                {
                    await context.RespondAsync(new BaseFailedResponseContract { Reason = exception.ErrorCode });
                }
            }

            public void Probe(ProbeContext context)
            {
                context.CreateFilterScope("appExceptionResponse");
            }
        }
    }
}
