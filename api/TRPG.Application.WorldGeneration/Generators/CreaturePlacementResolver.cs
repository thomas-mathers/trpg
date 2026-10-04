using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public sealed record PlacementObstacle(Placement Placement, Footprint Footprint);

public static class CreaturePlacementResolver
{
    public static readonly Footprint Body = new(Width: 0.6, Depth: 0.6);

    private const double Clearance = 0.1;
    private const double BehindOffset = 0.45;
    private const double SearchStep = 0.5;
    private const double SearchRadius = 10;
    private const int FreeSpotTries = 50;
    private const double AngleStep = Math.PI / 12;

    private static readonly PlanarPoint[] SearchOffsets = BuildSearchOffsets();

    public static Placement PlaceAt(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        Placement preferred
    ) =>
        FindNear(frame, obstacles, preferred)
        ?? FindFree(frame, obstacles, SeedFrom(preferred))
        ?? Clamp(frame, preferred);

    public static Placement PlaceNear(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        Placement anchor,
        int seed
    )
    {
        var nearAnchor = FindNear(frame, obstacles, anchor);

        if (nearAnchor is null)
        {
            return PlaceFree(frame, obstacles, seed);
        }

        return nearAnchor with
        {
            Angle = FacingAngle(nearAnchor, anchor),
        };
    }

    public static Placement? PlaceBehind(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        Placement counter,
        Footprint counterSize
    )
    {
        var distance = (counterSize.Depth / 2) + BehindOffset;
        var candidate = counter with
        {
            X = counter.X - (Math.Sin(counter.Angle) * distance),
            Y = counter.Y + (Math.Cos(counter.Angle) * distance),
        };

        return IsFree(frame, obstacles.Select(ObstacleBox).ToArray(), candidate) ? candidate : null;
    }

    public static Placement PlaceFree(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        int seed
    )
    {
        var center = new Placement(frame.Width / 2, frame.Depth / 2, 0);

        return FindFree(frame, obstacles, seed)
            ?? FindNear(frame, obstacles, center)
            ?? Clamp(frame, center);
    }

    private static Placement? FindNear(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        Placement origin
    )
    {
        var boxes = obstacles.Select(ObstacleBox).ToArray();

        return CandidatesAround(origin)
            .FirstOrDefault(candidate => IsFree(frame, boxes, candidate));
    }

    private static Placement? FindFree(
        Footprint frame,
        IReadOnlyList<PlacementObstacle> obstacles,
        int seed
    )
    {
        var boxes = obstacles.Select(ObstacleBox).ToArray();
        var random = new Random(seed);
        var halfBody = Body.Width / 2;

        for (var attempt = 0; attempt < FreeSpotTries; attempt++)
        {
            var candidate = new Placement(
                Snap(halfBody + (random.NextDouble() * (frame.Width - Body.Width))),
                Snap(halfBody + (random.NextDouble() * (frame.Depth - Body.Depth))),
                random.Next(24) * AngleStep
            );

            if (IsFree(frame, boxes, candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<Placement> CandidatesAround(Placement origin) =>
        SearchOffsets.Select(offset => new Placement(
            origin.X + offset.X,
            origin.Y + offset.Y,
            origin.Angle
        ));

    private static bool IsFree(Footprint frame, OrientedBox[] obstacles, Placement candidate)
    {
        var body = OrientedBox.From(candidate, Body);

        return body.IsInside(frame.Width, frame.Depth)
            && !obstacles.Any(obstacle => obstacle.Overlaps(body));
    }

    private static OrientedBox ObstacleBox(PlacementObstacle obstacle) =>
        OrientedBox.From(obstacle.Placement, obstacle.Footprint).Inflated(Clearance);

    private static Placement Clamp(Footprint frame, Placement placement)
    {
        var halfBody = Body.Width / 2;

        return placement with
        {
            X = Math.Clamp(placement.X, halfBody, Math.Max(halfBody, frame.Width - halfBody)),
            Y = Math.Clamp(placement.Y, halfBody, Math.Max(halfBody, frame.Depth - halfBody)),
        };
    }

    private static double FacingAngle(Placement from, Placement target) =>
        Math.Atan2(target.X - from.X, -(target.Y - from.Y));

    private static double Snap(double value) => Math.Round(value / 0.25) * 0.25;

    private static int SeedFrom(Placement placement) =>
        unchecked(((int)Math.Round(placement.X * 4) * 397) ^ (int)Math.Round(placement.Y * 4));

    private static PlanarPoint[] BuildSearchOffsets()
    {
        var steps = (int)(SearchRadius / SearchStep);

        return Enumerable
            .Range(-steps, (2 * steps) + 1)
            .SelectMany(
                column => Enumerable.Range(-steps, (2 * steps) + 1),
                (column, row) => new PlanarPoint(column * SearchStep, row * SearchStep)
            )
            .Where(offset =>
                Math.Sqrt((offset.X * offset.X) + (offset.Y * offset.Y)) <= SearchRadius
            )
            .OrderBy(offset => (offset.X * offset.X) + (offset.Y * offset.Y))
            .ThenBy(offset => offset.X)
            .ThenBy(offset => offset.Y)
            .ToArray();
    }
}
