using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing.Queries;

public sealed record CreatureJourneyPosition(
    Point Position,
    IReadOnlyList<Point> Path,
    double MetersPerGameSecond,
    bool LeavesAtEnd,
    GameInstant? PausedAt
);

public sealed class GetCreatureJourneyPositionsQuery
{
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

internal sealed class GetCreatureJourneyPositionsQueryHandler(
    IRoutingDbContext routing,
    ICreaturesDbContext creatures,
    IWorldsDbContext worlds,
    IOptions<WorldClockOptions> clockOptions
)
    : IQueryHandler<
        GetCreatureJourneyPositionsQuery,
        IReadOnlyDictionary<Guid, CreatureJourneyPosition>
    >
{
    public async Task<IReadOnlyDictionary<Guid, CreatureJourneyPosition>> Handle(
        GetCreatureJourneyPositionsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var requestedMembers = await routing
            .JourneyMembers.AsNoTracking()
            .Where(member => query.CreatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        if (requestedMembers.Length == 0)
        {
            return new Dictionary<Guid, CreatureJourneyPosition>();
        }

        var journeyIds = requestedMembers.Select(member => member.JourneyId).Distinct().ToArray();
        var journeys = await routing
            .Journeys.AsNoTracking()
            .Where(journey =>
                journeyIds.AsEnumerable().Contains(journey.Id)
                && journey.Status == JourneyStatus.Traveling
            )
            .ToDictionaryAsync(journey => journey.Id, cancellationToken);
        if (journeys.Count == 0)
        {
            return new Dictionary<Guid, CreatureJourneyPosition>();
        }

        var activeJourneyIds = journeys.Keys.ToArray();
        var members = await routing
            .JourneyMembers.AsNoTracking()
            .Where(member => activeJourneyIds.AsEnumerable().Contains(member.JourneyId))
            .ToArrayAsync(cancellationToken);
        var legs = await routing
            .JourneyLegs.AsNoTracking()
            .Where(leg => activeJourneyIds.AsEnumerable().Contains(leg.JourneyId))
            .OrderBy(leg => leg.Index)
            .ToArrayAsync(cancellationToken);
        var memberIds = members
            .Where(member => journeys.ContainsKey(member.JourneyId))
            .Select(member => member.CreatureId)
            .Distinct()
            .ToArray();
        var speedsByCreatureId = await creatures
            .Creatures.AsNoTracking()
            .Where(creature => memberIds.AsEnumerable().Contains(creature.Id))
            .ToDictionaryAsync(
                creature => creature.Id,
                creature => creature.MovementSpeed,
                cancellationToken
            );
        var nodeIds = legs.SelectMany(leg => new[] { leg.FromNodeId, leg.ToNodeId })
            .Distinct()
            .ToArray();
        var locationsByNodeId = await worlds
            .TravelNodes.AsNoTracking()
            .Where(node => nodeIds.AsEnumerable().Contains(node.Id))
            .ToDictionaryAsync(node => node.Id, node => node.LocationId, cancellationToken);

        return requestedMembers
            .Where(member => journeys.ContainsKey(member.JourneyId))
            .Select(member =>
                ToPosition(
                    member,
                    members,
                    journeys,
                    legs,
                    speedsByCreatureId,
                    locationsByNodeId,
                    query
                )
            )
            .Where(position => position is not null)
            .ToDictionary(position => position!.CreatureId, position => position!.Position);
    }

    private CreaturePosition? ToPosition(
        JourneyMember member,
        IReadOnlyCollection<JourneyMember> members,
        IReadOnlyDictionary<Guid, Journey> journeys,
        IReadOnlyCollection<JourneyLeg> allLegs,
        IReadOnlyDictionary<Guid, float> speedsByCreatureId,
        IReadOnlyDictionary<Guid, Guid> locationsByNodeId,
        GetCreatureJourneyPositionsQuery query
    )
    {
        var journey = journeys[member.JourneyId];
        var legs = allLegs.Where(leg => leg.JourneyId == journey.Id).ToArray();
        var pace = PaceForJourney(journey.Id, members, speedsByCreatureId);
        var projection = JourneyProgress.Project(journey, legs, pace, query.GameTime);
        if (projection.Position is not { } position || projection.LegIndex >= legs.Length)
        {
            return null;
        }

        var locationLegs = LegsInLocation(
            legs,
            projection.LegIndex,
            query.LocationId,
            locationsByNodeId
        );
        if (locationLegs.Count == 0)
        {
            return null;
        }

        var path = RemainingPath(locationLegs, projection.LegProgressMeters, position);
        if (path.Count < 2)
        {
            return null;
        }

        var leavesAtEnd = projection.LegIndex + locationLegs.Count < legs.Length;
        return new CreaturePosition(
            member.CreatureId,
            new CreatureJourneyPosition(position, path, pace, leavesAtEnd, journey.PausedAt)
        );
    }

    private double PaceForJourney(
        Guid journeyId,
        IReadOnlyCollection<JourneyMember> members,
        IReadOnlyDictionary<Guid, float> speedsByCreatureId
    ) =>
        InLocationPace.MetersPerGameSecond(
            members
                .Where(member => member.JourneyId == journeyId)
                .Select(member => speedsByCreatureId[member.CreatureId])
                .Min(),
            clockOptions.Value.TimeScale
        );

    private static IReadOnlyList<JourneyLeg> LegsInLocation(
        IReadOnlyList<JourneyLeg> legs,
        int startIndex,
        Guid locationId,
        IReadOnlyDictionary<Guid, Guid> locationsByNodeId
    ) =>
        legs.Skip(startIndex)
            .TakeWhile(leg =>
                locationsByNodeId[leg.FromNodeId] == locationId
                && locationsByNodeId[leg.ToNodeId] == locationId
            )
            .ToArray();

    private static IReadOnlyList<Point> RemainingPath(
        IReadOnlyList<JourneyLeg> locationLegs,
        double currentLegProgressMeters,
        Point currentPosition
    )
    {
        var path = new List<Point> { currentPosition };
        AppendPoints(path, UntravelledPoints(locationLegs[0], currentLegProgressMeters));
        foreach (var leg in locationLegs.Skip(1))
        {
            AppendPoints(path, leg.Path.Points);
        }

        return path;
    }

    private static IEnumerable<Point> UntravelledPoints(JourneyLeg leg, double progressMeters)
    {
        var points = leg.Path.Points;
        var remaining =
            leg.Distance <= 0
                ? 0
                : Math.Clamp(progressMeters / leg.Distance, 0, 1) * PathLength(points);
        for (var index = 0; index < points.Count - 1; index++)
        {
            var segmentLength = Distance(points[index], points[index + 1]);
            if (remaining < segmentLength)
            {
                return points.Skip(index + 1);
            }

            remaining -= segmentLength;
        }

        return [];
    }

    private static void AppendPoints(List<Point> path, IEnumerable<Point> points)
    {
        foreach (var point in points)
        {
            if (path[^1] != point)
            {
                path.Add(point);
            }
        }
    }

    private static double PathLength(IReadOnlyList<Point> points) =>
        points.Zip(points.Skip(1)).Sum(segment => Distance(segment.First, segment.Second));

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private sealed record CreaturePosition(Guid CreatureId, CreatureJourneyPosition Position);
}
