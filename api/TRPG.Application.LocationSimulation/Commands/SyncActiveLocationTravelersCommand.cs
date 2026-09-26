using TRPG.Application.Common.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncActiveLocationTravelersCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveLocationTravelersCommandHandler(
    ICommandHandler<SyncLocationTravelersCommand> syncLocationTravelers
) : ICommandHandler<SyncActiveLocationTravelersCommand>
{
    public async Task Handle(
        SyncActiveLocationTravelersCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var player in ActiveLocations.Distinct(command.Players))
        {
            await syncLocationTravelers.Handle(
                new SyncLocationTravelersCommand
                {
                    WorldId = command.WorldId,
                    LocationId = player.LocationId,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }
    }
}
