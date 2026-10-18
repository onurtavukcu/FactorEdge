using FactorEdge.Application.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace FactorEdge.Infrastructure
{
    public sealed class AppTransaction(IDbContextTransaction transaction) : IAppTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }
    }
}
