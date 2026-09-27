using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveExitConnectorCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class ResolveExitConnectorCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeyItemIdsByOwner,
    IQueryHandler<GetActivatedTriggerIdsQuery, IReadOnlySet<Guid>> getActivatedTriggerIds,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors
) : ICommandHandler<ResolveExitConnectorCommand, Guid?>
{
    private const string OutsideExitLabel = "Outside";

    public async Task<Guid?> Handle(
        ResolveExitConnectorCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        var connectors = await getConnectorsByLocationId.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        if (connectors.Count == 0)
        {
            return null;
        }

        var playerKeyItemIds = await getKeyItemIdsByOwner.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );

        var activatedTriggerIds = await getActivatedTriggerIds.Handle(
            new GetActivatedTriggerIdsQuery { WorldId = command.WorldId },
            cancellationToken
        );

        var accessibleConnectorIds = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = playerKeyItemIds,
                ActivatedTriggerIds = activatedTriggerIds,
                GameTime = command.GameTime,
                ConnectorIds = connectors.Select(connector => connector.Id).ToArray(),
            },
            cancellationToken
        );

        var openExits = connectors
            .Where(connector => accessibleConnectorIds.Contains(connector.Id))
            .ToArray();

        if (openExits.Length == 0)
        {
            return null;
        }

        var outsideExit = openExits.FirstOrDefault(connector =>
            connector.DestinationLabel == OutsideExitLabel
        );

        return outsideExit?.DestinationLocationId
            ?? openExits[Random.Shared.Next(openExits.Length)].DestinationLocationId;
    }
}
