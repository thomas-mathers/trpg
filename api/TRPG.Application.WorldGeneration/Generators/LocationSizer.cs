using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomSizingRequest(
    BuildingType BuildingType,
    RoomRole? Role,
    IReadOnlyCollection<string> PropAssetKeys,
    int Capacity
);

internal static class LocationSizer
{
    internal const double GridSize = 0.25;
    internal const double HallwayWidth = 2;
    internal const double StreetWidth = 6;
    internal const double BuildingGap = 3;
    internal const double DistrictBuildingMargin = 6;

    private const double PropAreaFactor = 1.5;
    private const double AreaPerOccupant = 1.2;
    private const double BuildingCirculationFactor = 1.15;
    private const double DistrictSlackFactor = 2;
    private const double MinimumDistrictSide = 20;
    private const double HallwayBaseDepth = 2;
    private const double HallwayDepthPerRoom = 1.5;

    internal static Footprint SizeRoom(RoomSizingRequest request, Random random)
    {
        var limits = RoomSizeCatalog.Get(request.BuildingType, request.Role);
        var propArea = request.PropAssetKeys.Sum(key =>
            PropFootprintCatalog.Get(key).Footprint.Area()
        );
        var area = Math.Max(
            limits.MinimumArea,
            PropAreaFactor * propArea + AreaPerOccupant * request.Capacity
        );
        var aspect = 1.0 + random.NextDouble() * 0.6;

        return FromAreaAndAspect(Math.Min(area, limits.MaximumArea), aspect, limits.MaximumArea);
    }

    internal static Footprint SizeHallway(int roomCount) =>
        new(Width: HallwayWidth, Depth: SnapUp(HallwayBaseDepth + HallwayDepthPerRoom * roomCount));

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

    internal static Footprint SizeDistrict(
        IReadOnlyCollection<Footprint> buildingFootprints,
        double seatArea
    )
    {
        var buildingArea = buildingFootprints.Sum(footprint =>
            (footprint.Width + DistrictBuildingMargin) * (footprint.Depth + DistrictBuildingMargin)
        );
        var side = Math.Max(
            MinimumDistrictSide,
            Math.Sqrt(DistrictSlackFactor * (buildingArea + seatArea))
        );
        var widestBuilding = buildingFootprints
            .Select(footprint => footprint.Width)
            .DefaultIfEmpty(0)
            .Max();
        var deepestBuilding = buildingFootprints
            .Select(footprint => footprint.Depth)
            .DefaultIfEmpty(0)
            .Max();

        return new Footprint(
            Width: SnapUp(Math.Max(side, widestBuilding + 2 * BuildingGap)),
            Depth: SnapUp(Math.Max(side, StreetWidth + 2 * (deepestBuilding + BuildingGap)))
        );
    }

    internal static Footprint SizeWilderness() => WildernessCatalog.Size;

    internal static double SnapUp(double value) => Math.Ceiling(value / GridSize - 1e-9) * GridSize;

    internal static double SnapDown(double value) => Math.Floor(value / GridSize + 1e-9) * GridSize;

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

    private static double Area(this Footprint footprint) => footprint.Width * footprint.Depth;
}
