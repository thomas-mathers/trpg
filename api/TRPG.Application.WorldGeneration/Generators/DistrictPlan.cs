using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DistrictPlan(
    Footprint Size,
    IReadOnlyList<DistrictBuildingLayout> Buildings,
    IReadOnlyList<PlanRect> Streets,
    IReadOnlyList<PlanRect> Courts,
    PlanRect Square,
    double SideAxis
);

internal static class DistrictFacing
{
    internal static Footprint Rotated(Footprint footprint, int facing) =>
        facing % 2 == 1 ? new Footprint(footprint.Depth, footprint.Width) : footprint;

    internal static DistrictBuildingLayout Place(
        DistrictBuildingInput building,
        double left,
        double top,
        int facing
    )
    {
        var rotated = Rotated(building.Footprint, facing);
        var placement = new Placement(
            left + rotated.Width / 2,
            top + rotated.Depth / 2,
            facing * Math.PI / 2
        );

        return new DistrictBuildingLayout(
            building.Id,
            placement,
            DistrictLayoutGenerator.FrontDoorPoint(placement, building.Footprint)
        );
    }
}
