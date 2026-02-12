namespace MyStreamHistory.Shared.Base.Contracts.Viewers.Requests;

public class GetTopViewersRequestContract
{
    public string StreamerTwitchUserId { get; set; } = string.Empty;
    public int Limit { get; set; } = 100;
}

