using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncActiveLocationRoutinesCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveLocationRoutinesCommandHandler(
    ICommandHandler<SyncLocationRoutinesCommand> syncLocationRoutines
) : ICommandHandler<SyncActiveLocationRoutinesCommand>
{
    public async Task Handle(
        SyncActiveLocationRoutinesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var player in ActiveLocations.Distinct(command.Players))
        {
            await syncLocationRoutines.Handle(
                new SyncLocationRoutinesCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    LocationId = player.LocationId,
                    PlayerLevel = player.PlayerLevel,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }
    }
}
