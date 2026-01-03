using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyStreamHistory.Shared.Application.UnitOfWork;

namespace MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork
{
    public class UnitOfWork<TDbContext>(TDbContext dbContext) : IUnitOfWork
        where TDbContext : DbContext
    {
        private IDbContextTransaction? _transaction;

        private TDbContext Context => dbContext;

        public async Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            _transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            return new DatabaseTransaction(_transaction);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await Context.SaveChangesAsync(cancellationToken);
        }
    }
}
