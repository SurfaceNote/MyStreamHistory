namespace MyStreamHistory.API.Repositories
{
    using Microsoft.EntityFrameworkCore;
    using MyStreamHistory.API.Data;
    using MyStreamHistory.API.Models;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _appDbContext;

        public UserRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task<User> CreateStreamerAsync(User streamer)
        {
            _appDbContext.Users.Add(streamer);
            await _appDbContext.SaveChangesAsync();
            return streamer;
        }

        public async Task DeleteStreamerAsync(int id)
        {
            var streamer = await _appDbContext.Users.FindAsync(id);
            if (streamer != null)
            {
                _appDbContext.Users.Remove(streamer);
                await _appDbContext.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<User>> GetStreamersAsync()
        {
            return await _appDbContext.Users.ToListAsync();
        }

        public async Task<User?> GetStreamerByIdAsync(int id)
        {
            return await _appDbContext.Users.FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task UpdateStreamerAsync(User streamer)
        {
            _appDbContext.Entry(streamer).State = EntityState.Modified;
            await _appDbContext.SaveChangesAsync();
        }
    }
}
