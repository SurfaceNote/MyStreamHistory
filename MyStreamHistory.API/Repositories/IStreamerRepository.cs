namespace MyStreamHistory.API.Repositories
{
    using MyStreamHistory.API.Models;
    public interface IStreamerRepository
    {
        Task<IEnumerable<Streamer>> GetStreamersAsync();
        Task<Streamer?> GetStreamerByIdAsync(int id);
        Task<Streamer> CreateStreamerAsync(Streamer streamer);
        Task UpdateStreamerAsync(Streamer streamer);
        Task DeleteStreamerAsync(int id);
    }
}
