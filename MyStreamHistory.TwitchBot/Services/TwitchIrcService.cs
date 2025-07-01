namespace MyStreamHistory.TwitchBot.Services
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    public class TwitchIrcService
    {
        private readonly ILogger<TwitchIrcService> _logger;
        private TcpClient _tcpClient;
        private NetworkStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;
        private readonly object _viewersLock = new();

        public TwitchIrcService(ILogger<TwitchIrcService> logger)
        {
            _logger = logger;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync("irc.twitch.tv", 6667);
                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream, Encoding.UTF8);
                _writer = new StreamWriter(_stream, new UTF8Encoding(false)) { AutoFlush = true };

                await _writer.WriteLineAsync("PASS oauth:pass");
                await _writer.WriteLineAsync("NICK nick");
                await JoinChannelAsync("kination");
                _logger.LogInformation("Connected to Twitch IRC");

                _ = Task.Run(() => ReadMessagesAsync(cancellationToken));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to Twitch IRC");
            }
        }

        private async Task ReadMessagesAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var message = await _reader.ReadLineAsync();

                    if (string.IsNullOrEmpty(message))
                    {
                        continue;
                    }

                    if (message.StartsWith("PING"))
                    {
                        await _writer.WriteLineAsync("PONG :tmi.twitch.tv");
                        Console.WriteLine("Sent PONG");
                        _logger.LogDebug("Sent PONG");
                    }

                    Console.WriteLine(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading IRC messages");
            }
        }

        public async Task JoinChannelAsync(string channelName)
        {
            await _writer.WriteLineAsync($"JOIN #{channelName.ToLower()}");
            _logger.LogInformation($"JOIN #{channelName.ToLower()}");
        }

        public async Task PartChannelAsync(string channelName)
        {
            await _writer.WriteLineAsync($"PART #{channelName.ToLower()}");
            _logger.LogInformation($"PART #{channelName.ToLower()}");
        }
    }
}
