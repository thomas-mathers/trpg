using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.WorldGeneration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetNearestTempleSanctuaryFromLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid FromLocationId { get; init; }
}

public record NearestTemple(Guid SanctuaryLocationId, string CityName);

internal class GetNearestTempleSanctuaryFromLocationQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetNearestReachableLocationQuery, Guid?> getNearestReachableLocation
) : IQueryHandler<GetNearestTempleSanctuaryFromLocationQuery, NearestTemple?>
{
    public async Task<NearestTemple?> Handle(
        GetNearestTempleSanctuaryFromLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var cityNameBySanctuaryLocationId = await GetTempleCandidates(
            query.WorldId,
            cancellationToken
        );
        if (cityNameBySanctuaryLocationId.Count == 0)
        {
            return null;
        }

        var nearestSanctuaryLocationId = await getNearestReachableLocation.Handle(
            new GetNearestReachableLocationQuery
            {
                WorldId = query.WorldId,
                FromLocationId = query.FromLocationId,
                CandidateLocationIds = cityNameBySanctuaryLocationId.Keys.ToArray(),
            },
            cancellationToken
        );

        return nearestSanctuaryLocationId == null
            ? null
            : new NearestTemple(
                nearestSanctuaryLocationId.Value,
                cityNameBySanctuaryLocationId[nearestSanctuaryLocationId.Value]
            );
    }

    private async Task<Dictionary<Guid, string>> GetTempleCandidates(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var temples =
            from building in context.Buildings.AsNoTracking()
            join buildingLocation in context.Locations.AsNoTracking()
                on building.ExteriorLocationId equals buildingLocation.Id
            join room in context.Rooms.AsNoTracking() on building.Id equals room.BuildingId
            where
                building.WorldId == worldId
                && building.BuildingType == BuildingType.Temple
                && room.Name == TempleRoomNames.Sanctuary
                && buildingLocation.CityId != null
            select new
            {
                SanctuaryLocationId = room.LocationId,
                CityId = buildingLocation.CityId!.Value,
            };

        return await (
            from temple in temples
            join city in context.Cities.AsNoTracking() on temple.CityId equals city.Id
            select new { temple.SanctuaryLocationId, city.Name }
        ).ToDictionaryAsync(x => x.SanctuaryLocationId, x => x.Name, cancellationToken);
    }
}
