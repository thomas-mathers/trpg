namespace TRPG.Application.Common.Concurrency;

public interface IWorldMutationGate
{
    Task<IAsyncDisposable> Acquire(Guid worldId, CancellationToken cancellationToken = default);
}
