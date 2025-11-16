using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyStreamHistory.Shared.Application.UnitOfWork
{
    public interface IUnitOfWork
    {
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task<IDatabaseTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
