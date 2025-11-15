using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamHistory.Shared.Infrastructure.Transport
{
    public class RabbitMqOptions
    {
        public string Host { get; set; } = default!;
        public string VirtualHost { get; set; } = "/";
        public string Username { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}
