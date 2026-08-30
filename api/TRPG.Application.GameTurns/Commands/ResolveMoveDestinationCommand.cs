using TRPG.Application.Buildings.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public class ResolveMoveDestinationCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required string DestinationName { get; init; }
}

public record ResolveMoveDestinationResult(EntryOutcome Outcome, Guid? DestinationLocationId);

internal class ResolveMoveDestinationCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetExitByDestinationNameQuery, ExitMatch> getExitByDestinationName,
    ICommandHandler<SyncFrontDoorLockCommand> syncFrontDoorLock,
    ICommandHandler<
        ResolveAccessibleConnectorsCommand,
        IReadOnlyCollection<Guid>
    > resolveAccessibleConnectors,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<GetKeyItemIdsByOwnerQuery, IReadOnlySet<Guid>> getKeyItemIdsByOwner
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

        var exitMatch = await getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = player.LocationId,
                DestinationName = command.DestinationName,
            },
            cancellationToken
        );

        if (!exitMatch.Matched)
        {
            return new ResolveMoveDestinationResult(
                currentLocation!.RoomId == null
                    ? EntryOutcome.DestinationNotFound
                    : EntryOutcome.ExitNotFound,
                null
            );
        }

        var destinationLocationId = exitMatch.DestinationLocationId!.Value;
        var connectorId = exitMatch.ConnectorId!.Value;

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        var currentDate = GameClock.GetCurrentInGameDate(playtime);

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

        var accessibleConnectorIds = await resolveAccessibleConnectors.Handle(
            new ResolveAccessibleConnectorsCommand
            {
                PlayerKeyItemIds = playerKeyItemIds,
                Playtime = playtime,
                ConnectorIds = [connectorId],
            },
            cancellationToken
        );

        return accessibleConnectorIds.Contains(connectorId)
            ? new ResolveMoveDestinationResult(EntryOutcome.Entered, destinationLocationId)
            : new ResolveMoveDestinationResult(EntryOutcome.Locked, null);
    }
}
