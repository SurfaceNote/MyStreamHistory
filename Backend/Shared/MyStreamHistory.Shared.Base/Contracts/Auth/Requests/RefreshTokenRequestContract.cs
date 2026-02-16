namespace MyStreamHistory.Shared.Base.Contracts.Auth.Requests;

public class RefreshTokenRequestContract
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = null!;
}