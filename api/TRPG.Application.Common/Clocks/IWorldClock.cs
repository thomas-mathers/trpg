using TRPG.Domain;

namespace TRPG.Application.Common.Clocks;

public interface IWorldClock
{
    Task<GameInstant> GetCurrent(Guid worldId, CancellationToken cancellationToken = default);
    Task<GameInstant> ResumeWorld(Guid worldId, CancellationToken cancellationToken = default);
    Task<GameInstant> PauseWorld(Guid worldId, CancellationToken cancellationToken = default);
    Task<GameInstant> Advance(
        Guid worldId,
        TimeSpan duration,
        CancellationToken cancellationToken = default
    );
    Task<GameInstant> Checkpoint(Guid worldId, CancellationToken cancellationToken = default);
    IReadOnlyCollection<Guid> GetActiveWorldIds();
    Task CheckpointActiveWorlds(CancellationToken cancellationToken = default);
}
