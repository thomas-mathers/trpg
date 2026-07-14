using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Tools;

internal class MoveTool(
    GameSession session,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    GetCreatureByIdQueryHandler getCreatureById,
    UpdateCreatureCommandHandler updateCreature,
    GetBuildingByNameInStateQueryHandler getBuildingByNameInState,
    GetEntranceRoomQueryHandler getEntranceRoom,
    GetExitByDestinationNameQueryHandler getExitByDestinationName,
    GetDistrictByNameInCityQueryHandler getDistrictByNameInCity,
    GetCityByStateIdQueryHandler getCityByStateId,
    CanEnterBuildingQueryHandler canEnterBuilding,
    SyncScheduleLockCommandHandler syncScheduleLock,
    ILogger<MoveTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("move")]
    [Description(
        "Moves the player to a destination by exact name and returns the full scene there — do not call look after moving. When outdoors, pass the exact Name of a building from NearbyBuildings to enter it, or the exact Name of a district from City.Districts to travel there. When indoors, pass the exact DestinationRoomName of an exit from Room.Exits to travel through it (this includes the literal value \"Outside\" for exits that lead outdoors). The name must be copied verbatim from the most recent look or move result — never invented, guessed, or paraphrased, and never a name you have not actually seen in a tool result this session."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of a nearby building, the exact Name of a district, or the exact DestinationRoomName of an exit (the literal value \"Outside\" for exits leading outdoors), copied verbatim from the most recent look or move result."
        )]
            string destinationName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[move] destinationName={DestinationName}", destinationName);
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        var error =
            player!.RoomId == null
                ? await MoveOutdoors(player, destinationName, cancellationToken)
                : await MoveIndoors(player, destinationName, cancellationToken);

        if (error != null)
        {
            return error;
        }

        await updateCreature.Handle(
            new UpdateCreatureCommand { Creature = player! },
            cancellationToken
        );
        session.DidMoveThisTurn = true;

        var currentDate = GameClock.GetCurrentInGameDate(session);
        var result = await getSceneWithCatchUp.Handle(
            new GetSceneWithCatchUpQuery
            {
                Session = session,
                RoomId = player.RoomId,
                DistrictId = player.DistrictId,
                StateId = player.StateId,
                CurrentDate = currentDate,
            },
            cancellationToken
        );

        session.DidSceneRefreshThisTurn = true;
        logger.LogInformation(
            "[perf] [move] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }

    private async Task<object?> MoveOutdoors(
        Creature player,
        string destinationName,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByNameInState.Handle(
            new GetBuildingByNameInStateQuery { StateId = player.StateId, Name = destinationName },
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
                return new
                {
                    Error = $"'{destinationName}' has no entrance. Call look to see what's around.",
                };
            }

            var currentDate = GameClock.GetCurrentInGameDate(session);
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
                return new { Error = $"The door to '{destinationName}' is locked." };
            }

            player.CityId = building.CityId;
            player.DistrictId = building.DistrictId;
            player.RoomId = entranceRoom.Id;
            return null;
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
                        Name = destinationName,
                    },
                    cancellationToken
                )
                : null;
        if (district != null)
        {
            player.CityId = cityId;
            player.DistrictId = district.Id;
            return null;
        }

        return new
        {
            Error = $"No building or district named '{destinationName}' found nearby. Call look to see what's around.",
        };
    }

    private async Task<object?> MoveIndoors(
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
            return new
            {
                Error = $"No exit named '{destinationName}' found here. Call look to see the available exits.",
            };
        }

        player.RoomId = exitMatch.DestinationRoomId;
        return null;
    }
}
