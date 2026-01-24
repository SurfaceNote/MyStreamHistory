namespace MyStreamHistory.Shared.Base.Contracts.TwitchEventSub;

public class UserRegisteredEventContract
{
    public int TwitchUserId { get; set; }
    public string DisplayName { get; set; } = null!;
    public string Login { get; set; } = null!;
}

