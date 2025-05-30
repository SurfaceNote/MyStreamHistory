﻿namespace MyStreamHistory.API.DTOs
{
    using MyStreamHistory.API.Enums;
    public class StreamerDTO
    {
        public int TwitchId { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public ChannelStatusEnum BroadcasterType { get; set; }
        public string? LogoUser { get; set; }
    }
}
