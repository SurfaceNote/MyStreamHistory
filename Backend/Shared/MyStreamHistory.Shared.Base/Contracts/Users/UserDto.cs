namespace MyStreamHistory.Shared.Base.Contracts.Users;

public class UserDto
{
    public int TwitchId { get; set; }

    public string DisplayName { get; set; } = null!;
    
    public string Avatar { get; set; } = null!;
}