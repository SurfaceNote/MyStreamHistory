namespace MyStreamHistory.TwitchBot.Services
{
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class TwitchBotService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TwitchIrcService _twitchIrcService;

        public TwitchBotService(IHttpClientFactory httpClientFactory, TwitchIrcService twitchIrcService)
        {
            _httpClientFactory = httpClientFactory;
            _twitchIrcService = twitchIrcService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _twitchIrcService.ConnectAsync(stoppingToken);
        }

    }
}
