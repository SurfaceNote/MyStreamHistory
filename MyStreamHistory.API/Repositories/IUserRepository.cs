namespace MyStreamHistory.API.Repositories
{
    using MyStreamHistory.API.Models;
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetStreamersAsync();
        Task<User?> GetStreamerByIdAsync(int id);
        Task<User?> GetStreamerByTwitchIdAsync(int twitchId);
        Task<User> CreateUserAsync(User streamer);
        Task UpdateUserAsync(User streamer);
        Task DeleteStreamerAsync(int id);
    }
}
