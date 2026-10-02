using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class ResolveMoveDestinationCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required GameInstant GameTime { get; init; }
}

public record ResolveMoveDestinationResult(EntryOutcome Outcome, Guid? DestinationLocationId);

internal class ResolveMoveDestinationCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetConnectorsByLocationIdQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByLocationId,
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeyItemIdsByOwner,
    IQueryHandler<GetActivatedTriggerIdsQuery, IReadOnlySet<Guid>> getActivatedTriggerIds
) : ICommandHandler<ResolveMoveDestinationCommand, ResolveMoveDestinationResult>
{
    public async Task<ResolveMoveDestinationResult> Handle(
        ResolveMoveDestinationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player!.LocationId },
            cancellationToken
        );

        var connectors = await getConnectorsByLocationId.Handle(
            new GetConnectorsByLocationIdQuery { LocationId = player.LocationId },
            cancellationToken
        );
        var connector = connectors.FirstOrDefault(c => c.Id == command.ConnectorId);

        if (connector == null)
        {
            return new ResolveMoveDestinationResult(
                currentLocation!.RoomId == null
                    ? EntryOutcome.DestinationNotFound
                    : EntryOutcome.ExitNotFound,
                null
            );
        }

        var destinationLocationId = connector.DestinationLocationId;
        var connectorId = connector.Id;

        var currentDate = GameClock.GetCurrentInGameDate(command.GameTime);

        await syncFrontDoorLock.Handle(
            new SyncFrontDoorLockCommand
            {
                LocationId = destinationLocationId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        var playerKeyItemIds = await getKeyItemIdsByOwner.Handle(
            new GetKeyItemIdsByOwnerQuery
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            },
            cancellationToken
        );

        var activatedTriggerIds = await getActivatedTriggerIds.Handle(
            new GetActivatedTriggerIdsQuery { WorldId = player.WorldId },
            cancellationToken
        );

        var accessibleConnectorIds = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = playerKeyItemIds,
                ActivatedTriggerIds = activatedTriggerIds,
                GameTime = command.GameTime,
                ConnectorIds = [connectorId],
            },
            cancellationToken
        );

        if (!accessibleConnectorIds.Contains(connectorId))
        {
            return new ResolveMoveDestinationResult(EntryOutcome.Locked, null);
        }

        return new ResolveMoveDestinationResult(EntryOutcome.Entered, destinationLocationId);
    }
}
