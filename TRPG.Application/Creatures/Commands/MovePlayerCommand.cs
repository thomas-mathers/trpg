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

internal enum MovePlayerOutcome
{
    Moved,
    BuildingHasNoEntrance,
    DoorLocked,
    DestinationNotFound,
    ExitNotFound,
}

internal class MovePlayerCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required string DestinationName { get; init; }
}

internal record MovePlayerResult(MovePlayerOutcome Outcome, Creature Player);

internal class MovePlayerCommandHandler(
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreaturesAtLocationQueryHandler getCreaturesAtLocation,
    UpdateCreaturesCommandHandler updateCreatures,
    DeleteCreaturesCommandHandler deleteCreatures,
    GetBuildingByNameInStateQueryHandler getBuildingByNameInState,
    GetEntranceRoomQueryHandler getEntranceRoom,
    GetExitByDestinationNameQueryHandler getExitByDestinationName,
    GetDistrictByNameInCityQueryHandler getDistrictByNameInCity,
    GetCityByStateIdQueryHandler getCityByStateId,
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
        var oldRoomId = player!.RoomId;
        var oldDistrictId = player.DistrictId;

        var outcome =
            player.RoomId == null
                ? await MoveOutdoors(player, command, cancellationToken)
                : await MoveIndoors(player, command.DestinationName, cancellationToken);

        if (outcome != MovePlayerOutcome.Moved)
        {
            return new MovePlayerResult(outcome, player);
        }

        await CleanUpDeadCreatures(
            player.WorldId,
            player.StateId,
            oldRoomId,
            oldDistrictId,
            cancellationToken
        );

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [player.Id],
                CityId = Optional<Guid?>.Of(player.CityId),
                DistrictId = Optional<Guid?>.Of(player.DistrictId),
                RoomId = Optional<Guid?>.Of(player.RoomId),
            },
            cancellationToken
        );

        return new MovePlayerResult(MovePlayerOutcome.Moved, player);
    }

    private async Task CleanUpDeadCreatures(
        Guid worldId,
        Guid stateId,
        Guid? oldRoomId,
        Guid? oldDistrictId,
        CancellationToken cancellationToken
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                Location = CreatureLocation.Of(worldId, stateId, oldRoomId, oldDistrictId),
            },
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

    private async Task<MovePlayerOutcome> MoveOutdoors(
        Creature player,
        MovePlayerCommand command,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByNameInState.Handle(
            new GetBuildingByNameInStateQuery
            {
                StateId = player.StateId,
                Name = command.DestinationName,
            },
            cancellationToken
        );
        if (building != null)
        {
            var entranceRoom = await getEntranceRoom.Handle(
                new GetEntranceRoomQuery { BuildingId = building.Id },
                cancellationToken
            );
            if (entranceRoom == null)
            {
                return MovePlayerOutcome.BuildingHasNoEntrance;
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
            var canEnter = await canEnterBuilding.Handle(
                new CanEnterBuildingQuery
                {
                    EntranceRoomId = entranceRoom.Id,
                    EnteringCreatureId = player.Id,
                },
                cancellationToken
            );
            if (!canEnter)
            {
                return MovePlayerOutcome.DoorLocked;
            }

            player.CityId = building.CityId;
            player.DistrictId = building.DistrictId;
            player.RoomId = entranceRoom.Id;
            return MovePlayerOutcome.Moved;
        }

        var cityId =
            player.CityId
            ?? (
                await getCityByStateId.Handle(
                    new GetCityByStateIdQuery { StateId = player.StateId },
                    cancellationToken
                )
            )?.Id;
        var district =
            cityId != null
                ? await getDistrictByNameInCity.Handle(
                    new GetDistrictByNameInCityQuery
                    {
                        CityId = cityId.Value,
                        Name = command.DestinationName,
                    },
                    cancellationToken
                )
                : null;
        if (district != null)
        {
            player.CityId = cityId;
            player.DistrictId = district.Id;
            return MovePlayerOutcome.Moved;
        }

        return MovePlayerOutcome.DestinationNotFound;
    }

    private async Task<MovePlayerOutcome> MoveIndoors(
        Creature player,
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var exitMatch = await getExitByDestinationName.Handle(
            new GetExitByDestinationNameQuery
            {
                RoomId = player.RoomId!.Value,
                DestinationName = destinationName,
            },
            cancellationToken
        );
        if (!exitMatch.Matched)
        {
            return MovePlayerOutcome.ExitNotFound;
        }

        player.RoomId = exitMatch.DestinationRoomId;
        return MovePlayerOutcome.Moved;
    }
}
