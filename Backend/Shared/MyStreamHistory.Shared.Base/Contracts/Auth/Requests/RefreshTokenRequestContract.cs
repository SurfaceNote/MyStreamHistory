namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class RefreshTokenRequestContract
{
    public string Token { get; set; } = null!;
}