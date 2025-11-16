using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public class MassTransitOptions
    {
        public string EndpointPrefix { get; set; } = string.Empty;
        public bool UseInMemoryOutbox { get; set; } = true;
        public bool UseRetry { get; set; } = true;
        public int[] RetryIntervals { get; set; } = [100, 500, 1000];
    }
}
