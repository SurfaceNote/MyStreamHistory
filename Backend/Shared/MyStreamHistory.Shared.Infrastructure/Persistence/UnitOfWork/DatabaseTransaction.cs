using Microsoft.EntityFrameworkCore.Storage;
using MyStreamHistory.Shared.Application.UnitOfWork;

namespace MyStreamHistory.Shared.Infrastructure.Persistence.UnitOfWork
{
    public class DatabaseTransaction(IDbContextTransaction transaction) : IDatabaseTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return transaction.RollbackAsync(cancellationToken);
        }
    }
}
