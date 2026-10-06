using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomSizingRequest(
    BuildingType BuildingType,
    RoomRole? Role,
    IReadOnlyCollection<PropModel> PropModels,
    int Capacity
);

internal static class LocationSizer
{
    internal const double GridSize = RoomGrid.CellSize;

    private const double PropAreaFactor = 1.5;
    private const double AreaPerOccupant = 1.2;
    private const double BuildingCirculationFactor = 1.15;

    internal static Footprint SizeRoom(RoomSizingRequest request, Random random)
    {
        var limits = RoomSizeCatalog.Get(request.BuildingType, request.Role);
        var propArea = request.PropModels.Sum(model =>
            RoomGrid.CellArea(PropFootprintCatalog.Get(model).Footprint)
        );
        var area = Math.Max(
            limits.MinimumArea,
            PropAreaFactor * propArea + AreaPerOccupant * request.Capacity
        );
        var aspect = 1.0 + random.NextDouble() * 0.6;

        return FromAreaAndAspect(Math.Min(area, limits.MaximumArea), aspect, limits.MaximumArea);
    }

    internal static Footprint SizeBuilding(
        BuildingType buildingType,
        double groundFloorArea,
        Random random
    )
    {
        var area = Math.Max(
            BuildingFootprintCatalog.GetMinimumArea(buildingType),
            groundFloorArea * BuildingCirculationFactor
        );
        var aspect = 1.2 + random.NextDouble() * 0.2;

        return FromAreaAndAspect(area, aspect, double.MaxValue);
    }

    internal static Footprint SizeWilderness() => WildernessCatalog.Size;

    internal static double SnapUp(double value) => RoomGrid.SnapUp(value);

    internal static double SnapDown(double value) => RoomGrid.SnapDown(value);

    private static Footprint FromAreaAndAspect(double area, double aspect, double maximumArea)
    {
        var width = SnapUp(Math.Sqrt(area * aspect));
        var depth = SnapUp(area / width);

        if (width * depth > maximumArea)
        {
            depth = Math.Max(GridSize, SnapDown(maximumArea / width));
        }

        return new Footprint(Width: width, Depth: depth);
    }
}
