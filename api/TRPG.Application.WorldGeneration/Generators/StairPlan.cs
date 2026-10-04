namespace TRPG.Application.WorldGeneration.Generators;

internal static class StairPlan
{
    internal const double FlightSpacing = 1.2;

    internal static ConnectorExit Exit(Guid connectorId, double roomWidth, int lowerFloorNumber)
    {
        var offset = lowerFloorNumber % 2 == 0 ? -FlightSpacing / 2 : FlightSpacing / 2;

        return new ConnectorExit(connectorId, new PlanarPoint(roomWidth / 2 + offset, 0), Math.PI);
    }
}
