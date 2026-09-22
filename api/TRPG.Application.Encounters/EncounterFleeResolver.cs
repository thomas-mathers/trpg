using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal class EncounterFleeResolver(
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectors,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeys,
    IQueryHandler<GetActivatedTriggerIdsQuery, IReadOnlySet<Guid>> getActivatedTriggerIds,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors,
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
            encounter.DepartureDestinationLocationId != null
            && await TryResumeDeparture(encounter, player, playtime, cancellationToken)
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

    // Returns false rather than throwing when the specific saved destination has become
    // unreachable, so the caller can fall back to any other way out instead of stranding the
    // player over a since-locked door.
    private async Task<bool> TryResumeDeparture(
        Encounter encounter,
        Creature player,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        var connectors = await getConnectors.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        var matchingIds = connectors
            .Where(connector =>
                connector.DestinationLocationId == encounter.DepartureDestinationLocationId
            )
            .Select(connector => connector.Id)
            .ToArray();
        if (!await IsDepartureAccessible(encounter, matchingIds, playtime, cancellationToken))
        {
            return false;
        }

        await MoveTo(
            player.Id,
            encounter.DepartureDestinationLocationId!.Value,
            playtime,
            cancellationToken
        );
        return true;
    }

    private async Task<bool> IsDepartureAccessible(
        Encounter encounter,
        IReadOnlyCollection<Guid> connectorIds,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        if (connectorIds.Count == 0)
        {
            return false;
        }

        var keys = await getKeys.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(encounter.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        var activatedTriggerIds = await getActivatedTriggerIds.Handle(
            new GetActivatedTriggerIdsQuery { WorldId = encounter.WorldId },
            cancellationToken
        );
        var accessible = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                ConnectorIds = connectorIds,
                PlayerKeyItemIds = keys,
                ActivatedTriggerIds = activatedTriggerIds,
                Playtime = playtime,
            },
            cancellationToken
        );
        return accessible.Count > 0;
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
