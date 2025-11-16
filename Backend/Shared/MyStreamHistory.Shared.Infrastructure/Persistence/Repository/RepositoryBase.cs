using Microsoft.EntityFrameworkCore;
using MyStreamHistory.Shared.Application.Repository;
using System.Linq.Expressions;

namespace MyStreamHistory.Shared.Infrastructure.Persistence.Repository
{
    public class RepositoryBase<TEntity, TDbContext>(TDbContext context) : IRepositoryBase<TEntity>
        where TEntity : class
        where TDbContext : DbContext
    {
        private readonly DbSet<TEntity> _set = context.Set<TEntity>();

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            await _set.AddAsync(entity, cancellationToken);
            return entity;
        }

        public Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
        {
            return _set.AddRangeAsync(entities, cancellationToken);
        }

        public Task<List<TEntity>> GetAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default)
        {
            return _set.Where(filter).ToListAsync(cancellationToken);
        }

        public Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return _set.FindAsync(new[] { id }, cancellationToken).AsTask();
        }

        public IQueryable<TEntity> Query()
        {
            return _set.AsQueryable();
        }

        public void Remove(TEntity entity)
        {
            _set.Remove(entity);
        }

        public void RemoveRange(IEnumerable<TEntity> entities)
        {
            _set.RemoveRange(entities);
        }

        public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            _set.Update(entity);
            return Task.CompletedTask;
        }
    }
}
