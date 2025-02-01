namespace MyStreamHistory.API.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Data;
    using MyStreamHistory.API.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class StreamerRepository : IStreamerRepository
    {
        private readonly AppDbContext _appDbContext;

        public StreamerRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<Streamer> CreateStreamerAsync(Streamer streamer)
        {
            _appDbContext.Streamers.Add(streamer);
            await _appDbContext.SaveChangesAsync();
            return streamer;
        }

        public async Task DeleteStreamerAsync(int id)
        {
            var streamer = await _appDbContext.Streamers.FindAsync(id);
            if (streamer != null)
            {
                _appDbContext.Streamers.Remove(streamer);
                await _appDbContext.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<Streamer>> GetStreamersAsync()
        {
            return await _appDbContext.Streamers.ToListAsync();
        }

        public async Task<Streamer?> GetStreamerByIdAsync(int id)
        {
            return await _appDbContext.Streamers.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task UpdateStreamerAsync(Streamer streamer)
        {
            _appDbContext.Entry(streamer).State = EntityState.Modified;
            await _appDbContext.SaveChangesAsync();
        }
    }
}
