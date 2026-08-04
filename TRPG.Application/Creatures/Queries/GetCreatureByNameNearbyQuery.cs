using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureByNameNearbyQuery
{
    public required Guid WorldId { get; init; }
    public required Creature Player { get; init; }
    public required string Name { get; init; }
}

internal class GetCreatureByNameNearbyQueryHandler(
    GetCreatureByNameAtLocationQueryHandler getByNameAtLocation
)
{
    public async Task<Creature?> Handle(
        GetCreatureByNameNearbyQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = query.Player;

        return await getByNameAtLocation.Handle(
            new GetCreatureByNameAtLocationQuery
            {
                WorldId = query.WorldId,
                LocationId = player.LocationId,
                Name = query.Name,
            },
            cancellationToken
        );
    }
}
