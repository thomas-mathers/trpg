using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class WildernessBuildingPlacer
{
    private const double EdgeMargin = 10;
    private const double BuildingSpacing = 6;
    private const double ScanStep = 5;
    private const int SamplingTries = 100;

    internal static IReadOnlyList<DistrictBuildingLayout> Place(
        Footprint wilderness,
        IReadOnlyCollection<DistrictBuildingInput> buildings,
        Random random
    )
    {
        var layouts = new List<DistrictBuildingLayout>();
        var occupied = new List<OrientedBox>();
        var ordered = buildings
            .OrderByDescending(building => building.Footprint.Width * building.Footprint.Depth)
            .ThenBy(building => building.Id);

        foreach (var building in ordered)
        {
            var placement =
                SampleFreePlacement(wilderness, building.Footprint, occupied, random)
                ?? ScanForFreePlacement(wilderness, building.Footprint, occupied)
                ?? new Placement(wilderness.Width / 2, wilderness.Depth / 2, 0);

            occupied.Add(OrientedBox.From(placement, building.Footprint));
            layouts.Add(
                new DistrictBuildingLayout(
                    building.Id,
                    placement,
                    DistrictLayoutGenerator.FrontDoorPoint(placement, building.Footprint)
                )
            );
        }

        return layouts;
    }

    private static Placement? SampleFreePlacement(
        Footprint wilderness,
        Footprint footprint,
        IReadOnlyList<OrientedBox> occupied,
        Random random
    )
    {
        for (var attempt = 0; attempt < SamplingTries; attempt++)
        {
            var candidate = new Placement(
                LocationSizer.SnapUp(
                    EdgeMargin + random.NextDouble() * (wilderness.Width - 2 * EdgeMargin)
                ),
                LocationSizer.SnapUp(
                    EdgeMargin + random.NextDouble() * (wilderness.Depth - 2 * EdgeMargin)
                ),
                random.Next(4) * Math.PI / 2
            );

            if (IsFree(wilderness, OrientedBox.From(candidate, footprint), occupied))
            {
                return candidate;
            }
        }

        return null;
    }

    private static Placement? ScanForFreePlacement(
        Footprint wilderness,
        Footprint footprint,
        IReadOnlyList<OrientedBox> occupied
    )
    {
        for (var y = EdgeMargin; y < wilderness.Depth - EdgeMargin; y += ScanStep)
        {
            for (var x = EdgeMargin; x < wilderness.Width - EdgeMargin; x += ScanStep)
            {
                var candidate = new Placement(x, y, 0);

                if (IsFree(wilderness, OrientedBox.From(candidate, footprint), occupied))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static bool IsFree(
        Footprint wilderness,
        OrientedBox box,
        IReadOnlyList<OrientedBox> occupied
    )
    {
        var spaced = box.Inflated(BuildingSpacing / 2);

        return box.Inflated(EdgeMargin).IsInside(wilderness.Width, wilderness.Depth)
            && occupied.All(other => !spaced.Overlaps(other.Inflated(BuildingSpacing / 2)));
    }
}
