namespace MyStreamHistory.API.Repositories
{
    using MyStreamHistory.API.Models;
    public interface IUserRepository
    {
        Task<IEnumerable<User>> GetStreamersAsync();
        Task<User?> GetStreamerByIdAsync(int id);
        Task<User> CreateStreamerAsync(User streamer);
        Task UpdateStreamerAsync(User streamer);
        Task DeleteStreamerAsync(int id);
    }
}
