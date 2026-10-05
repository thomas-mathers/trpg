using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class DistrictConnectorGenerator
{
    public static IReadOnlyList<LocationConnector> Generate(
        IReadOnlyList<District> districts,
        Guid worldId
    )
    {
        var cells = DistrictGrid.Assign(districts);
        var byCell = districts.ToDictionary(district => cells[district.LocationId]);

        return districts
            .SelectMany(origin =>
                DistrictGrid
                    .Edges.Select(edge =>
                        byCell.TryGetValue(cells[origin.LocationId].Step(edge), out var neighbor)
                            ? Connect(origin, neighbor, edge, worldId)
                            : null
                    )
                    .OfType<LocationConnector>()
            )
            .ToArray();
    }

    private static LocationConnector Connect(
        District origin,
        District destination,
        CompassDirection direction,
        Guid worldId
    ) =>
        new()
        {
            OriginLocationId = origin.LocationId,
            DestinationLocationId = destination.LocationId,
            Name = "Path",
            Description = $"A path leading to {destination.Name}.",
            DestinationLabel = destination.Name,
            Direction = direction,
            WorldId = worldId,
        };
}
