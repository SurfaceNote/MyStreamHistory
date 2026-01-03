namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class TwitchAuthorizeRequestContract
{
    public string Code { get; init; } = null!;
    public string State { get; init; } = null!;
}