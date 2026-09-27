using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal class EncounterDepartureResolver(
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
    ICommandHandler<MovePlayerCommand> movePlayer
)
{
    public async Task<bool> TryResume(
        Encounter encounter,
        Creature player,
        GameInstant gameTime,
        CancellationToken cancellationToken = default
    )
    {
        if (player.WorldId != encounter.WorldId || player.LocationId != encounter.LocationId)
        {
            throw new InvalidOperationException(
                "The player is no longer at the encounter location."
            );
        }

        if (encounter.DepartureDestinationLocationId is not { } destinationLocationId)
        {
            return false;
        }

        var connectorIds = await GetMatchingConnectorIds(
            player.LocationId,
            destinationLocationId,
            cancellationToken
        );
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
        var accessibleConnectorIds = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                ConnectorIds = connectorIds,
                PlayerKeyItemIds = keys,
                ActivatedTriggerIds = activatedTriggerIds,
                GameTime = gameTime,
            },
            cancellationToken
        );
        if (accessibleConnectorIds.Count == 0)
        {
            return false;
        }

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                DestinationLocationId = destinationLocationId,
                GameTime = gameTime,
            },
            cancellationToken
        );
        return true;
    }

    private async Task<IReadOnlyCollection<Guid>> GetMatchingConnectorIds(
        Guid locationId,
        Guid destinationLocationId,
        CancellationToken cancellationToken
    )
    {
        var connectors = await getConnectors.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );
        return connectors
            .Where(connector => connector.DestinationLocationId == destinationLocationId)
            .Select(connector => connector.Id)
            .ToArray();
    }
}
