using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class StairPlan
{
    internal const double FlightSpacing = 1.2;
    internal const double Width = 1.2;
    internal const double Depth = 2;
    internal const double ArrivalInset = Depth + 0.5;

    internal static ConnectorExit Exit(
        Guid connectorId,
        double roomWidth,
        int lowerFloorNumber,
        StairDirection direction
    ) =>
        new(
            connectorId,
            new PlanarPoint(
                roomWidth / 2
                    + (lowerFloorNumber % 2 == 0 ? -FlightSpacing / 2 : FlightSpacing / 2),
                0
            ),
            Math.PI
        )
        {
            Stairs = direction,
        };

    internal static ConnectorExit ThroughExit(
        Guid connectorId,
        Footprint frame,
        StairDirection direction,
        int slot,
        int slotCount
    )
    {
        var x = frame.Width * (slot + 1) / (slotCount + 1);
        var exit =
            direction == StairDirection.Up
                ? new ConnectorExit(connectorId, new PlanarPoint(x, frame.Depth), 0)
                : new ConnectorExit(connectorId, new PlanarPoint(x, 0), Math.PI);

        return exit with
        {
            Stairs = direction,
        };
    }
}
