using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal class EncounterFleeResolver(
    EncounterDepartureResolver encounterDepartureResolver,
    ICommandHandler<ResolveExitConnectorCommand, Guid?> resolveExitConnector,
    ICommandHandler<MovePlayerCommand> movePlayer
)
{
    public async Task<bool> Resolve(
        Encounter encounter,
        Creature player,
        TimeSpan playtime,
        CancellationToken cancellationToken = default
    )
    {
        if (player.WorldId != encounter.WorldId || player.LocationId != encounter.LocationId)
        {
            throw new InvalidOperationException(
                "The player is no longer at the encounter location."
            );
        }

        if (
            await encounterDepartureResolver.TryResume(
                encounter,
                player,
                playtime,
                cancellationToken
            )
        )
        {
            return true;
        }

        var exitLocationId = await resolveExitConnector.Handle(
            new ResolveExitConnectorCommand
            {
                WorldId = encounter.WorldId,
                PlayerId = player.Id,
                Playtime = playtime,
            },
            cancellationToken
        );
        if (exitLocationId != null)
        {
            await MoveTo(player.Id, exitLocationId.Value, playtime, cancellationToken);
            return true;
        }

        if (player.PreviousLocationId is { } originLocationId)
        {
            await MoveTo(player.Id, originLocationId, playtime, cancellationToken);
            return true;
        }

        return false;
    }

    private Task MoveTo(
        Guid playerId,
        Guid destinationLocationId,
        TimeSpan playtime,
        CancellationToken cancellationToken
    ) =>
        movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = playerId,
                DestinationLocationId = destinationLocationId,
                Playtime = playtime,
            },
            cancellationToken
        );
}
