using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetNearbyCorpsesQuery
{
    public required Guid PlayerId { get; init; }
}

internal class GetNearbyCorpsesQueryHandler(
    TrpgDbContext context,
    GetAllNearbyCreaturesQueryHandler getAllNearbyCreatures
)
{
    public async Task<IReadOnlyList<CreatureSummary>> Handle(
        GetNearbyCorpsesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.PlayerId, cancellationToken);

        var nearby = await getAllNearbyCreatures.Handle(
            new GetAllNearbyCreaturesQuery
            {
                Location = new CreatureLocation(
                    player.WorldId,
                    player.RoomId,
                    player.StateId,
                    player.DistrictId
                ),
                ExcludingCreatureId = query.PlayerId,
            },
            cancellationToken
        );

        return nearby.Where(c => c.State == CreatureState.Dead).ToArray();
    }
}
