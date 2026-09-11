using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal class DepartureMovementResumer(
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectors,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeys,
    IQueryHandler<GetPulledLeverIdsQuery, IReadOnlySet<Guid>> getLevers,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors,
    ICommandHandler<MovePlayerCommand> movePlayer
)
{
    public async Task Resume(
        Encounter encounter,
        Creature player,
        TimeSpan playtime,
        CancellationToken cancellationToken = default
    )
    {
        if (player.WorldId != encounter.WorldId || player.LocationId != encounter.LocationId)
        {
            throw new InvalidOperationException(
                "The player is no longer at the interrupted departure location."
            );
        }

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
        await ValidateAccess(encounter, matchingIds, playtime, cancellationToken);

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = player.Id,
                DestinationLocationId = encounter.DepartureDestinationLocationId!.Value,
                Playtime = playtime,
            },
            cancellationToken
        );
    }

    private async Task ValidateAccess(
        Encounter encounter,
        IReadOnlyCollection<Guid> connectorIds,
        TimeSpan playtime,
        CancellationToken cancellationToken
    )
    {
        if (connectorIds.Count == 0)
        {
            throw new InvalidOperationException(
                "The interrupted destination is no longer connected to this location."
            );
        }

        var keys = await getKeys.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(encounter.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );
        var levers = await getLevers.Handle(
            new GetPulledLeverIdsQuery { WorldId = encounter.WorldId },
            cancellationToken
        );
        var accessible = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                ConnectorIds = connectorIds,
                PlayerKeyItemIds = keys,
                PulledLeverIds = levers,
                Playtime = playtime,
            },
            cancellationToken
        );
        if (accessible.Count == 0)
        {
            throw new InvalidOperationException(
                "The way to the interrupted destination is now locked."
            );
        }
    }
}
