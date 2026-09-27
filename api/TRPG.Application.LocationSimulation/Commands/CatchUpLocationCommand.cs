using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class CatchUpLocationCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class CatchUpLocationCommandHandler(
    ICommandHandler<SyncLocationTravelersCommand> syncLocationTravelers,
    ICommandHandler<SyncLocationRoutinesCommand, SyncLocationRoutinesResult> syncLocationRoutines
) : ICommandHandler<CatchUpLocationCommand>
{
    public async Task Handle(
        CatchUpLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await syncLocationTravelers.Handle(
            new SyncLocationTravelersCommand
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        await syncLocationRoutines.Handle(
            new SyncLocationRoutinesCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                PlayerLevel = command.PlayerLevel,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
