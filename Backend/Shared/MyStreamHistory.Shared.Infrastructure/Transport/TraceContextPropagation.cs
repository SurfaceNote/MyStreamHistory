using System.Diagnostics;
using MassTransit;
using MassTransit.Logging;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public static class TraceContextPropagation
    {
        /// <summary>
        /// Writes the current trace context into every sent and published message.
        /// </summary>
        /// <remarks>
        /// MassTransit adds <c>MT-Activity-Id</c> only from its own send activity, and OpenTelemetry does not
        /// create child activities for traces that were not sampled. Without this filter the consumer of an
        /// unsampled request starts a new trace, and its logs get a TraceId different from the caller's.
        /// For sampled traces MassTransit replaces the header with its send activity, as before.
        /// </remarks>
        public static void PropagateTraceContext(this IBusFactoryConfigurator configurator)
        {
            configurator.ConfigureSend(send => send.UseExecute(SetTraceContext));
            configurator.ConfigurePublish(publish => publish.UseExecute(SetTraceContext));
        }

        private static void SetTraceContext(SendContext context)
        {
            if (Activity.Current?.Id is { } activityId
                && !context.Headers.TryGetHeader(DiagnosticHeaders.ActivityId, out _))
            {
                context.Headers.Set(DiagnosticHeaders.ActivityId, activityId);
            }
        }
    }
}
