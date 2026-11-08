namespace MyStreamHistory.Shared.Api.DTOs
{
    public class TwitchCallbackResponse
    {
        public string AccessToken { get; set; } = null!;
        public bool IsNewUser { get; set; }
    }
}
