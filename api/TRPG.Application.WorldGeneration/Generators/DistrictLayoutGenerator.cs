using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DistrictBuildingInput(Guid Id, BuildingType Type, Footprint Footprint);

internal record DistrictSeatInput(Guid Id, Footprint Footprint);

internal record DistrictBuildingLayout(Guid Id, Placement Placement, PlanarPoint DoorPoint);

internal record DistrictSeatLayout(Guid Id, Placement Placement);

internal record DistrictLayout(
    DistrictPlan Plan,
    IReadOnlyList<DistrictSeatLayout> Seats,
    IReadOnlyList<DistrictDecor> Decor
)
{
    internal Footprint District => Plan.Size;

    internal IReadOnlyList<DistrictBuildingLayout> Buildings => Plan.Buildings;
}

internal static class DistrictLayoutGenerator
{
    internal static DistrictLayout Generate(
        DistrictType type,
        IReadOnlyCollection<DistrictBuildingInput> buildings,
        IReadOnlyCollection<DistrictSeatInput> seats,
        int seed
    )
    {
        var plan =
            type == DistrictType.Residential
                ? ResidentialDistrictPlanner.Plan(buildings, new Random(seed))
                : PrecinctDistrictPlanner.Plan(type, buildings);
        var furnishing = OutdoorFurnisher.Furnish(type, plan, buildings, seats);

        return new DistrictLayout(plan, furnishing.Seats, furnishing.Decor);
    }

    internal static PlanarPoint FrontDoorPoint(Placement placement, Footprint footprint)
    {
        var distance = footprint.Depth / 2;

        return new PlanarPoint(
            placement.X + distance * Math.Sin(placement.Angle),
            placement.Y - distance * Math.Cos(placement.Angle)
        );
    }
}
