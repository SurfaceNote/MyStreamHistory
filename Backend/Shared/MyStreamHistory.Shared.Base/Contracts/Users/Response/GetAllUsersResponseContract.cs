namespace MyStreamHistory.Shared.Base.Contracts.Users.Response;

public class GetAllUsersResponseContract
{
    public List<UserDto> Users { get; set; } = [];
}