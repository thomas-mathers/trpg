using System.Collections.Concurrent;
using TRPG.Application.Routing.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

public sealed class TransientCreatureWalkRegistry
{
    private readonly ConcurrentDictionary<Guid, TransientWalk> _walks = new();

    public void Set(
        Guid creatureId,
        Guid locationId,
        IReadOnlyList<Point> path,
        GameInstant startedAt,
        double metersPerGameSecond
    ) =>
        _walks[creatureId] = new TransientWalk(
            locationId,
            [.. path],
            startedAt,
            metersPerGameSecond
        );

    public void Remove(Guid creatureId) => _walks.TryRemove(creatureId, out _);

    public CreatureJourneyPosition? Find(Guid creatureId, Guid locationId, GameInstant now)
    {
        if (!_walks.TryGetValue(creatureId, out var walk) || walk.LocationId != locationId)
        {
            return null;
        }

        var walked = Math.Max(0, (now - walk.StartedAt).TotalSeconds) * walk.MetersPerGameSecond;
        var (position, segmentIndex) = PositionAt(walk.Path, walked);
        var remaining = new[] { position }.Concat(walk.Path.Skip(segmentIndex + 1)).ToArray();
        return new CreatureJourneyPosition(
            position,
            remaining.Length > 1 ? remaining : [position, position],
            walk.MetersPerGameSecond,
            false,
            null
        );
    }

    private static (Point Position, int SegmentIndex) PositionAt(
        IReadOnlyList<Point> path,
        double walked
    )
    {
        for (var index = 0; index < path.Count - 1; index++)
        {
            var length = Distance(path[index], path[index + 1]);
            if (walked <= length)
            {
                var fraction = length == 0 ? 1 : walked / length;
                return (
                    new Point(
                        path[index].X + (path[index + 1].X - path[index].X) * fraction,
                        path[index].Y + (path[index + 1].Y - path[index].Y) * fraction
                    ),
                    index
                );
            }
            walked -= length;
        }

        return (path[^1], path.Count - 1);
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));

    private sealed record TransientWalk(
        Guid LocationId,
        IReadOnlyList<Point> Path,
        GameInstant StartedAt,
        double MetersPerGameSecond
    );
}
