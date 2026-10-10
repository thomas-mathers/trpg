using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Commands;

public class SyncLocationTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncLocationTravelersCommandHandler : ICommandHandler<SyncLocationTravelersCommand>
{
    public async Task Handle(
        SyncLocationTravelersCommand command,
        CancellationToken cancellationToken = default
    ) { }
}
