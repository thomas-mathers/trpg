using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Common;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Commands;

internal class MovePlayerCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required string DestinationName { get; init; }
}

internal record MovePlayerResult(EntryOutcome Outcome, Creature Player);

internal class MovePlayerCommandHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    GetLocationByIdQueryHandler getLocationById,
    GetCreaturesAtLocationQueryHandler getCreaturesAtLocation,
    UpdateCreaturesCommandHandler updateCreatures,
    DeleteCreaturesCommandHandler deleteCreatures,
    GetBuildingByNameAtLocationQueryHandler getBuildingByNameAtLocation,
    GetExitByDestinationNameQueryHandler getExitByDestinationName,
    CanEnterBuildingQueryHandler canEnterBuilding,
    SyncScheduleLockCommandHandler syncScheduleLock,
    GetPlaytimeQueryHandler getPlaytime,
    ILogger<MovePlayerCommandHandler> logger
)
{
    public async Task<MovePlayerResult> Handle(
        MovePlayerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var oldLocationId = player!.LocationId;

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );

        var outcome = await MoveFromLocation(player, currentLocation!, command, cancellationToken);

        if (outcome != EntryOutcome.Entered)
        {
            return new MovePlayerResult(outcome, player);
        }

        await CleanUpDeadCreatures(player.WorldId, oldLocationId, cancellationToken);

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [player.Id],
                LocationId = player.LocationId,
            },
            cancellationToken
        );

        return new MovePlayerResult(EntryOutcome.Entered, player);
    }

    private async Task CleanUpDeadCreatures(
        Guid worldId,
        Guid oldLocationId,
        CancellationToken cancellationToken
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery { WorldId = worldId, LocationId = oldLocationId },
            cancellationToken
        );

        var deadCreatureIds = nearby
            .Where(creature => creature.State == CreatureState.Dead)
            .Select(creature => creature.Id)
            .ToArray();

        if (deadCreatureIds.Length == 0)
        {
            return;
        }

        logger.LogInformation(
            "[move] deleting {Count} dead creature(s) left behind: {CreatureIds}",
            deadCreatureIds.Length,
            string.Join(", ", deadCreatureIds)
        );

        await deleteCreatures.Handle(
            new DeleteCreaturesCommand { CreatureIds = deadCreatureIds },
            cancellationToken
        );
    }

    private async Task<EntryOutcome> MoveFromLocation(
        Creature player,
        Location currentLocation,
        MovePlayerCommand command,
        CancellationToken cancellationToken
    )
    {
        if (currentLocation.RoomId == null)
        {
            var buildingOutcome = await TryEnterBuildingByName(
                player,
                currentLocation,
                command,
                cancellationToken
            );
            if (buildingOutcome != null)
            {
                return buildingOutcome.Value;
            }
        }

        var exitMatch = await getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                LocationId = currentLocation.Id,
                DestinationName = command.DestinationName,
            },
            cancellationToken
        );

        if (!exitMatch.Matched)
        {
            return currentLocation.RoomId == null
                ? EntryOutcome.DestinationNotFound
                : EntryOutcome.ExitNotFound;
        }

        player.LocationId = exitMatch.DestinationLocationId!.Value;

        return EntryOutcome.Entered;
    }

    private async Task<EntryOutcome?> TryEnterBuildingByName(
        Creature player,
        Location currentLocation,
        MovePlayerCommand command,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByNameAtLocation.Handle(
            new GetBuildingByNameAtLocationQuery
            {
                Location = currentLocation,
                Name = command.DestinationName,
            },
            cancellationToken
        );

        if (building == null)
        {
            return null;
        }

        var schedulePlaytime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var currentDate = GameClock.GetCurrentInGameDate(schedulePlaytime);

        await syncScheduleLock.Handle(
            new SyncScheduleLockCommand
            {
                BuildingId = building.Id,
                BuildingType = building.BuildingType,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        var entry = await canEnterBuilding.Handle(
            new CanEnterBuildingQuery { BuildingId = building.Id, EnteringCreatureId = player.Id },
            cancellationToken
        );

        if (entry.Outcome != EntryOutcome.Entered)
        {
            return entry.Outcome;
        }

        player.LocationId = entry.EntranceLocationId!.Value;

        return EntryOutcome.Entered;
    }
}
