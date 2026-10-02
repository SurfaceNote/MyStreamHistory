namespace MyStreamHistory.Shared.Base.Contracts.Users;

public class UserDto
{
    public int TwitchId { get; set; }

    public string DisplayName { get; set; } = null!;
    
    public string Avatar { get; set; } = null!;

    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsLive { get; set; }
}
