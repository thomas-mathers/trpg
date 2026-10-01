using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DistrictBuildingInput(Guid Id, Footprint Footprint);

internal record DistrictSeatInput(Guid Id, Footprint Footprint);

internal record DistrictBuildingLayout(Guid Id, Placement Placement, PlanarPoint DoorPoint);

internal record DistrictSeatLayout(Guid Id, Placement Placement);

internal record DistrictLayout(
    Footprint District,
    IReadOnlyList<DistrictBuildingLayout> Buildings,
    IReadOnlyList<DistrictSeatLayout> Seats
);

internal static class DistrictLayoutGenerator
{
    private const double SeatStreetInset = 0.5;
    private const double SouthFacingAngle = Math.PI;
    private const double NorthFacingAngle = 0;

    internal static DistrictLayout Generate(
        IReadOnlyCollection<DistrictBuildingInput> buildings,
        IReadOnlyCollection<DistrictSeatInput> seats
    )
    {
        var sized = LocationSizer.SizeDistrict(
            buildings.Select(building => building.Footprint).ToArray(),
            seats.Sum(seat => seat.Footprint.Width * seat.Footprint.Depth)
        );
        var rows = SplitIntoRows(buildings);
        var width = Math.Max(
            sized.Width,
            LocationSizer.SnapUp(Math.Max(RowLength(rows.North), RowLength(rows.South)))
        );
        var district = new Footprint(Width: width, Depth: sized.Depth);
        var streetCenterY = district.Depth / 2;

        var placed = PlaceRow(
                rows.North,
                streetCenterY - LocationSizer.StreetWidth / 2,
                isNorthSide: true
            )
            .Concat(
                PlaceRow(
                    rows.South,
                    streetCenterY + LocationSizer.StreetWidth / 2,
                    isNorthSide: false
                )
            )
            .ToArray();

        return new DistrictLayout(district, placed, PlaceSeats(seats, district));
    }

    private static StreetRows SplitIntoRows(IReadOnlyCollection<DistrictBuildingInput> buildings)
    {
        var north = new List<DistrictBuildingInput>();
        var south = new List<DistrictBuildingInput>();
        var ordered = buildings
            .OrderByDescending(building => building.Footprint.Width * building.Footprint.Depth)
            .ThenBy(building => building.Id);

        foreach (var building in ordered)
        {
            var target = RowLength(north) <= RowLength(south) ? north : south;
            target.Add(building);
        }

        return new StreetRows(north, south);
    }

    private static double RowLength(IReadOnlyCollection<DistrictBuildingInput> row) =>
        row.Sum(building => building.Footprint.Width + LocationSizer.BuildingGap)
        + LocationSizer.BuildingGap;

    private static IEnumerable<DistrictBuildingLayout> PlaceRow(
        IReadOnlyList<DistrictBuildingInput> row,
        double streetEdgeY,
        bool isNorthSide
    )
    {
        var cursor = LocationSizer.BuildingGap;

        foreach (var building in row)
        {
            var footprint = building.Footprint;
            var centerX = cursor + footprint.Width / 2;
            var centerY = isNorthSide
                ? streetEdgeY - footprint.Depth / 2
                : streetEdgeY + footprint.Depth / 2;
            var angle = isNorthSide ? NorthFacingAngle : SouthFacingAngle;
            var placement = new Placement(X: centerX, Y: centerY, Angle: angle);

            yield return new DistrictBuildingLayout(
                building.Id,
                placement,
                FrontDoorPoint(placement, footprint)
            );
            cursor += footprint.Width + LocationSizer.BuildingGap;
        }
    }

    private static PlanarPoint FrontDoorPoint(Placement placement, Footprint footprint)
    {
        var distance = footprint.Depth / 2;

        return new PlanarPoint(
            placement.X - distance * Math.Sin(placement.Angle),
            placement.Y + distance * Math.Cos(placement.Angle)
        );
    }

    private static List<DistrictSeatLayout> PlaceSeats(
        IReadOnlyCollection<DistrictSeatInput> seats,
        Footprint district
    )
    {
        var streetTop = district.Depth / 2 - LocationSizer.StreetWidth / 2;
        var streetBottom = district.Depth / 2 + LocationSizer.StreetWidth / 2;
        var perEdge = (seats.Count + 1) / 2;
        var layouts = new List<DistrictSeatLayout>();
        var index = 0;

        foreach (var seat in seats.OrderBy(seat => seat.Id))
        {
            var onNorthEdge = index % 2 == 0;
            var slot = index / 2 + 1;
            var x = district.Width * slot / (perEdge + 1);
            var y = onNorthEdge
                ? streetTop + SeatStreetInset + seat.Footprint.Depth / 2
                : streetBottom - SeatStreetInset - seat.Footprint.Depth / 2;
            var angle = onNorthEdge ? SouthFacingAngle : NorthFacingAngle;

            layouts.Add(new DistrictSeatLayout(seat.Id, new Placement(X: x, Y: y, Angle: angle)));
            index++;
        }

        return layouts;
    }

    private record StreetRows(
        IReadOnlyList<DistrictBuildingInput> North,
        IReadOnlyList<DistrictBuildingInput> South
    );
}
