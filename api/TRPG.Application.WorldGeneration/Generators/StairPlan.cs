namespace TRPG.Application.WorldGeneration.Generators;

internal static class StairPlan
{
    internal const double FlightSpacing = 1.2;
    private const double DoorClearance = 0.75;

    internal static ConnectorExit Exit(Guid connectorId, double roomWidth, int lowerFloorNumber) =>
        new(connectorId, new PlanarPoint(FlightX(roomWidth, lowerFloorNumber), 0), Math.PI);

    internal static ConnectorExit FarExit(
        Guid connectorId,
        double roomWidth,
        double roomDepth,
        int lowerFloorNumber
    )
    {
        var x = Math.Clamp(
            FlightX(roomWidth, lowerFloorNumber),
            Math.Min(DoorClearance, roomWidth / 2),
            Math.Max(roomWidth - DoorClearance, roomWidth / 2)
        );

        return new ConnectorExit(connectorId, new PlanarPoint(x, roomDepth), 0);
    }

    private static double FlightX(double roomWidth, int lowerFloorNumber) =>
        roomWidth / 2 + (lowerFloorNumber % 2 == 0 ? -FlightSpacing / 2 : FlightSpacing / 2);
}
