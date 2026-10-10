using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Routing;

public sealed record JourneyProjection(int LegIndex, double LegProgressMeters, Point? Position);

public static class JourneyProgress
{
    public static JourneyProjection Project(
        Journey journey,
        IReadOnlyList<JourneyLeg> legs,
        double metersPerGameSecond,
        GameInstant at
    )
    {
        if (journey.Status != JourneyStatus.Traveling || journey.PausedAt != null)
        {
            return CheckpointProjection(journey, legs);
        }

        var progressedMeters =
            Math.Max(0, (at - journey.CheckpointedAt).TotalSeconds) * metersPerGameSecond;
        var legIndex = journey.CheckpointLegIndex;
        var legProgressMeters = journey.CheckpointLegProgressMeters;

        while (legIndex < legs.Count)
        {
            var remainingMeters = legs[legIndex].Distance - legProgressMeters;
            if (progressedMeters < remainingMeters)
            {
                legProgressMeters += progressedMeters;
                return new JourneyProjection(
                    legIndex,
                    legProgressMeters,
                    PositionAt(legs[legIndex], legProgressMeters)
                );
            }

            progressedMeters -= remainingMeters;
            legIndex++;
            legProgressMeters = 0;
        }

        return new JourneyProjection(legIndex, 0, null);
    }

    private static JourneyProjection CheckpointProjection(
        Journey journey,
        IReadOnlyList<JourneyLeg> legs
    ) =>
        journey.CheckpointLegIndex >= legs.Count
            ? new JourneyProjection(journey.CheckpointLegIndex, 0, null)
            : new JourneyProjection(
                journey.CheckpointLegIndex,
                journey.CheckpointLegProgressMeters,
                PositionAt(legs[journey.CheckpointLegIndex], journey.CheckpointLegProgressMeters)
            );

    private static Point? PositionAt(JourneyLeg leg, double progressMeters)
    {
        if (leg.Path.Points.Count == 0 || leg.Distance <= 0)
        {
            return null;
        }

        var distance = Math.Clamp(progressMeters / leg.Distance, 0, 1) * PathLength(leg.Path);
        return PointAt(leg.Path, distance);
    }

    private static double PathLength(Polyline path) =>
        path
            .Points.Zip(path.Points.Skip(1))
            .Sum(segment => Distance(segment.First, segment.Second));

    private static Point PointAt(Polyline path, double distance)
    {
        foreach (var segment in path.Points.Zip(path.Points.Skip(1)))
        {
            var length = Distance(segment.First, segment.Second);
            if (distance <= length)
            {
                var ratio = length == 0 ? 0 : distance / length;
                return new Point(
                    segment.First.X + (segment.Second.X - segment.First.X) * ratio,
                    segment.First.Y + (segment.Second.Y - segment.First.Y) * ratio
                );
            }

            distance -= length;
        }

        return path.Points[^1];
    }

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(second.X - first.X, 2) + Math.Pow(second.Y - first.Y, 2));
}
