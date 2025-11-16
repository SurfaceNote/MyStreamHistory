namespace MyStreamHistory.Shared.Infrastructure.Logging
{
    public class SerilogOptions
    {
        public string ApplicationName { get; set; } = "Unknown";
        public string? ElasticUrl { get; set; }
        public string? ElasticUsername { get; set; }
        public string? ElasticPassword { get; set; }
        public bool WriteToConsole { get; set; } = true;
        public bool WriteToElastic { get; set; } = false;
    }
}
