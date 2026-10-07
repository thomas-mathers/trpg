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
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocation
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
        if (
            !locations.TryGetValue(locationId, out var location)
            || location.Kind != LocationKind.Room
        )
        {
            return NpcPathPlanner.Straight;
        }

        var props = await getPropsByLocation.Handle(
            new GetPropsByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );

        return new NpcPathPlanner(RoomNavigationGrid.Build(location, props));
    }
}
