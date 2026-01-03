namespace MyStreamHistory.Shared.Base.Contracts.Auth.Responses;

public class RefreshTokenResponseContract
{
    public string AccessToken { get; init; } = null!;
    public string RefreshToken { get; init; } = null!;
}