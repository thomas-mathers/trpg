using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class HallwayDoorPlan
{
    private const int SideEast = 0;
    private const int SideWest = 1;

    internal static IReadOnlyList<ConnectorExit> Place(
        Footprint hallway,
        IReadOnlyList<Guid> connectorIds,
        IReadOnlyList<double> roomDepths
    )
    {
        var exits = new ConnectorExit[connectorIds.Count];

        foreach (var side in new[] { SideEast, SideWest })
        {
            var indexes = Enumerable
                .Range(0, connectorIds.Count)
                .Where(index => index % 2 == side)
                .ToArray();
            var centers = RoomCenters(hallway.Depth, indexes.Select(index => roomDepths[index]));

            for (var rank = 0; rank < indexes.Length; rank++)
            {
                exits[indexes[rank]] = new ConnectorExit(
                    connectorIds[indexes[rank]],
                    new PlanarPoint(side == SideEast ? hallway.Width : 0, centers[rank]),
                    side == SideEast ? 3 * Math.PI / 2 : Math.PI / 2
                );
            }
        }

        return exits;
    }

    private static double[] RoomCenters(double hallwayDepth, IEnumerable<double> depths)
    {
        var sideDepths = depths.ToArray();
        var gap = Math.Max(0, (hallwayDepth - sideDepths.Sum()) / (sideDepths.Length + 1));
        var centers = new double[sideDepths.Length];
        var cursor = gap;

        for (var index = 0; index < sideDepths.Length; index++)
        {
            centers[index] = cursor + sideDepths[index] / 2;
            cursor += sideDepths[index] + gap;
        }

        return centers;
    }
}
