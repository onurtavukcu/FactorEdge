namespace FactorEdge.Application.Interfaces
{
    public interface IAppTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
    }
}
