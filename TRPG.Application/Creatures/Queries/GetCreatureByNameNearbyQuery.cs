using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameNearbyQuery
{
    public required Guid WorldId { get; init; }
    public required Creature Player { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameNearbyQueryHandler(
    GetCreatureByNameAtLocationQueryHandler getByNameAtLocation,
    GetCreatureByNameOutdoorsInStateQueryHandler getByNameOutdoorsInState
)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameNearbyQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = query.Player;

        return player.LocationId is { } locationId
            ? await getByNameAtLocation.Handle(
                new GetCreatureByNameAtLocationQuery
                {
                    WorldId = query.WorldId,
                    LocationId = locationId,
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
