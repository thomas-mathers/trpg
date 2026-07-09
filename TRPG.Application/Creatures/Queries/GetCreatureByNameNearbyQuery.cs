using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameNearbyQuery
{
    public required Guid WorldId { get; init; }
    public required Creature Player { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameNearbyQueryHandler(
    GetCreatureByNameInRoomQueryHandler getByNameInRoom,
    GetCreatureByNameOutdoorsInDistrictQueryHandler getByNameOutdoorsInDistrict,
    GetCreatureByNameOutdoorsInStateQueryHandler getByNameOutdoorsInState
)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameNearbyQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = query.Player;

        if (player.RoomId is { } roomId)
        {
            return await getByNameInRoom.Handle(
                new GetCreatureByNameInRoomQuery
                {
                    WorldId = query.WorldId,
                    RoomId = roomId,
                    Name = query.Name,
                },
                cancellationToken
            );
        }

        return player.DistrictId is { } districtId
            ? await getByNameOutdoorsInDistrict.Handle(
                new GetCreatureByNameOutdoorsInDistrictQuery
                {
                    WorldId = query.WorldId,
                    DistrictId = districtId,
                    Name = query.Name,
                },
                cancellationToken
            )
            : await getByNameOutdoorsInState.Handle(
                new GetCreatureByNameOutdoorsInStateQuery
                {
                    WorldId = query.WorldId,
                    StateId = player.StateId,
                    Name = query.Name,
                },
                cancellationToken
            );
    }
}
