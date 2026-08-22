using Microsoft.EntityFrameworkCore;
using TRPG.Application.Buildings;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Algorithms;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetNearestTempleSanctuaryFromLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid FromLocationId { get; init; }
}

public record NearestTemple(Guid SanctuaryLocationId, string CityName);

internal record TempleCandidate(
    Guid SanctuaryLocationId,
    Guid CityEntranceLocationId,
    string CityName
);

internal class GetNearestTempleSanctuaryFromLocationQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetNearestTempleSanctuaryFromLocationQuery, NearestTemple?>
{
    public async Task<NearestTemple?> Handle(
        GetNearestTempleSanctuaryFromLocationQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var candidates = await GetTempleCandidates(query.WorldId, cancellationToken);
        if (candidates.Length == 0)
        {
            return null;
        }

        var edges = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector => connector.WorldId == query.WorldId)
            .Join(
                context.TravelConnectors.AsNoTracking(),
                connector => connector.Id,
                travel => travel.ConnectorId,
                (connector, travel) =>
                    new
                    {
                        connector.OriginLocationId,
                        connector.DestinationLocationId,
                        travel.Distance,
                    }
            )
            .ToArrayAsync(cancellationToken);

        var neighborsByOrigin = edges
            .GroupBy(edge => edge.OriginLocationId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IEnumerable<Guid>)group.Select(edge => edge.DestinationLocationId).ToArray()
            );
        var costByEdge = edges.ToDictionary(
            edge => (edge.OriginLocationId, edge.DestinationLocationId),
            edge => (double)edge.Distance
        );

        var startNode = await ResolveStartNode(
            query.WorldId,
            query.FromLocationId,
            neighborsByOrigin,
            cancellationToken
        );

        NearestTemple? nearest = null;
        var nearestCost = double.PositiveInfinity;

        foreach (var candidate in candidates)
        {
            var path = Graphs.ShortestPath(
                startNode,
                candidate.CityEntranceLocationId,
                locationId => neighborsByOrigin.GetValueOrDefault(locationId, []),
                (from, to) => costByEdge[(from, to)]
            );

            if (path.Count == 0)
            {
                continue;
            }

            var cost = SumPathCost(path, costByEdge);
            if (cost < nearestCost)
            {
                nearestCost = cost;
                nearest = new NearestTemple(candidate.SanctuaryLocationId, candidate.CityName);
            }
        }

        return nearest;
    }

    private async Task<TempleCandidate[]> GetTempleCandidates(
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
                CityId = buildingLocation.CityId!.Value,
                SanctuaryLocationId = room.LocationId,
            };

        var cityEntrances =
            from district in context.Districts.AsNoTracking()
            join city in context.Cities.AsNoTracking() on district.CityId equals city.Id
            where district.DistrictType == DistrictType.CityEntrance
            select new
            {
                district.CityId,
                CityEntranceLocationId = district.LocationId,
                CityName = city.Name,
            };

        return await (
            from temple in temples
            join entrance in cityEntrances on temple.CityId equals entrance.CityId
            select new TempleCandidate(
                temple.SanctuaryLocationId,
                entrance.CityEntranceLocationId,
                entrance.CityName
            )
        ).ToArrayAsync(cancellationToken);
    }

    private async Task<Guid> ResolveStartNode(
        Guid worldId,
        Guid fromLocationId,
        IReadOnlyDictionary<Guid, IEnumerable<Guid>> neighborsByOrigin,
        CancellationToken cancellationToken
    )
    {
        var fromLocationCityId = await context
            .Locations.AsNoTracking()
            .Where(location => location.Id == fromLocationId)
            .Select(location => location.CityId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fromLocationCityId != null)
        {
            var cityEntranceLocationId = await context
                .Districts.AsNoTracking()
                .Where(district =>
                    district.CityId == fromLocationCityId.Value
                    && district.DistrictType == DistrictType.CityEntrance
                )
                .Select(district => (Guid?)district.LocationId)
                .FirstOrDefaultAsync(cancellationToken);

            if (cityEntranceLocationId != null)
            {
                return cityEntranceLocationId.Value;
            }
        }

        if (neighborsByOrigin.ContainsKey(fromLocationId))
        {
            return fromLocationId;
        }

        var oneHopDestinationId = await context
            .LocationConnectors.AsNoTracking()
            .Where(connector =>
                connector.WorldId == worldId && connector.OriginLocationId == fromLocationId
            )
            .Select(connector => (Guid?)connector.DestinationLocationId)
            .FirstOrDefaultAsync(cancellationToken);

        return oneHopDestinationId ?? fromLocationId;
    }

    private static double SumPathCost(
        IReadOnlyList<Guid> path,
        IReadOnlyDictionary<(Guid, Guid), double> costByEdge
    )
    {
        var total = 0.0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            total += costByEdge[(path[i], path[i + 1])];
        }
        return total;
    }
}
