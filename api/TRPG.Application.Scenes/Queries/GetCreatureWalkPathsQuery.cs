using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Props.Queries;
using TRPG.Application.Scenes.Navigation;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

internal class GetCreatureWalkPathsQuery
{
    public required Guid LocationId { get; init; }
    public required IReadOnlyCollection<CreatureResult> Creatures { get; init; }
}

internal class GetCreatureWalkPathsQueryHandler(
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocation,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    IQueryHandler<GetRoadNetworkByLocationIdQuery, LocationRoadNetwork> getRoadNetwork
) : IQueryHandler<GetCreatureWalkPathsQuery, IReadOnlyDictionary<Guid, IReadOnlyList<Point>>>
{
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Point>>> Handle(
        GetCreatureWalkPathsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var walkers = query.Creatures.Where(CreaturePoseResolver.HasEntryWalk).ToArray();
        if (walkers.Length == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Point>>();
        }

        var planner = await BuildPlanner(query.LocationId, cancellationToken);

        return walkers.ToDictionary(walker => walker.Id, planner.Plan);
    }

    private async Task<NpcPathPlanner> BuildPlanner(
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [locationId] },
            cancellationToken
        );
        if (!locations.TryGetValue(locationId, out var location))
        {
            return NpcPathPlanner.Straight;
        }

        return location.Kind switch
        {
            LocationKind.Room => await BuildRoomPlanner(location, cancellationToken),
            LocationKind.District => await BuildDistrictPlanner(location, cancellationToken),
            _ => NpcPathPlanner.Straight,
        };
    }

    private async Task<NpcPathPlanner> BuildRoomPlanner(
        Location room,
        CancellationToken cancellationToken
    )
    {
        var props = await getPropsByLocation.Handle(
            new GetPropsByLocationIdQuery { LocationId = room.Id },
            cancellationToken
        );

        return NpcPathPlanner.ForRoom(RoomNavigationGrid.Build(room, props));
    }

    private async Task<NpcPathPlanner> BuildDistrictPlanner(
        Location district,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = district.Id },
            cancellationToken
        );
        var network = await getRoadNetwork.Handle(
            new GetRoadNetworkByLocationIdQuery { LocationId = district.Id },
            cancellationToken
        );

        return NpcPathPlanner.ForDistrict(
            network,
            DistrictNavigationGrid.Build(district, buildings)
        );
    }
}
