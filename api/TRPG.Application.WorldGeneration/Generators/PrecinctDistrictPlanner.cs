using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PrecinctDistrictPlanner
{
    private const double Gap = 4;
    private const double Margin = 7;
    private const double CourtPadding = 8;
    private const double MinimumCourtWidth = 28;
    private const double MinimumCourtDepth = 26;
    private const double MinimumSideDepth = 8;
    private const double AvenueWidth = 8;
    private const double AvenueDepth = 14;

    private static readonly IReadOnlyDictionary<DistrictType, BuildingType[]> Rosters =
        new Dictionary<DistrictType, BuildingType[]>
        {
            [DistrictType.CityCenter] =
            [
                BuildingType.GuildHall,
                BuildingType.Inn,
                BuildingType.Tavern,
                BuildingType.GeneralGoods,
                BuildingType.Bakery,
                BuildingType.Tailor,
                BuildingType.Carpenter,
                BuildingType.Jeweler,
            ],
            [DistrictType.Encampment] =
            [
                BuildingType.Barracks,
                BuildingType.Blacksmith,
                BuildingType.Stable,
            ],
            [DistrictType.Scientific] =
            [
                BuildingType.Library,
                BuildingType.ArcaneShop,
                BuildingType.Apothecary,
            ],
            [DistrictType.Governmental] = [BuildingType.Castle, BuildingType.Jail],
        };

    private static readonly BuildingType[] CityCenterTopRow =
    [
        BuildingType.GuildHall,
        BuildingType.Inn,
        BuildingType.Tavern,
    ];

    internal static DistrictPlan Plan(
        DistrictType type,
        IReadOnlyCollection<DistrictBuildingInput> buildings
    )
    {
        var ordered = Order(type, buildings);
        var topCount = TopRowCount(type, ordered);
        var north = ordered.Take(topCount).ToArray();
        var flanks = ordered.Skip(topCount).ToArray();
        var west = flanks.Where((_, index) => index % 2 == 0).ToArray();
        var east = flanks.Where((_, index) => index % 2 == 1).ToArray();

        return Arrange(north, west, east);
    }

    private static DistrictBuildingInput[] Order(
        DistrictType type,
        IReadOnlyCollection<DistrictBuildingInput> buildings
    )
    {
        var roster = Rosters.GetValueOrDefault(type, []);

        return buildings
            .OrderBy(building =>
            {
                var index = Array.IndexOf(roster, building.Type);
                return index < 0 ? roster.Length : index;
            })
            .ThenBy(building => building.Type)
            .ThenBy(building => building.Id)
            .ToArray();
    }

    private static int TopRowCount(DistrictType type, DistrictBuildingInput[] ordered) =>
        type == DistrictType.CityCenter
            ? ordered.Count(building => CityCenterTopRow.Contains(building.Type))
            : Math.Min(1, ordered.Length);

    private static DistrictPlan Arrange(
        DistrictBuildingInput[] north,
        DistrictBuildingInput[] west,
        DistrictBuildingInput[] east
    )
    {
        var sideDepth = Math.Max(
            MinimumSideDepth,
            west.Concat(east).Select(building => building.Footprint.Depth).DefaultIfEmpty(0).Max()
        );
        var topDepth = north.Select(building => building.Footprint.Depth).DefaultIfEmpty(0).Max();
        var courtWidth = LocationSizer.SnapUp(
            Math.Max(MinimumCourtWidth, Extent(north) + CourtPadding)
        );
        var courtDepth = LocationSizer.SnapUp(
            Math.Max(MinimumCourtDepth, Math.Max(Extent(west), Extent(east)) + CourtPadding)
        );
        var square = new PlanRect(
            LocationSizer.SnapUp(Margin + sideDepth),
            LocationSizer.SnapUp(Margin + topDepth),
            courtWidth,
            courtDepth
        );
        var buildings = PlaceNorth(north, square)
            .Concat(PlaceFlank(west, square, square.X, facing: 1))
            .Concat(PlaceFlank(east, square, square.Right, facing: 3))
            .ToArray();
        var size = new Footprint(
            Width: LocationSizer.SnapUp(square.X + courtWidth + square.X),
            Depth: LocationSizer.SnapUp(square.Bottom + AvenueDepth)
        );
        var avenue = new PlanRect(
            square.CenterX - AvenueWidth / 2,
            square.Bottom,
            AvenueWidth,
            AvenueDepth
        );
        var sideCourts = new[]
        {
            new PlanRect(Margin, square.Y, square.X - Margin, courtDepth),
            new PlanRect(square.Right, square.Y, square.X - Margin, courtDepth),
        };

        return new DistrictPlan(size, buildings, [square, avenue], sideCourts, square);
    }

    private static double Extent(IReadOnlyCollection<DistrictBuildingInput> row) =>
        row.Sum(building => building.Footprint.Width) + Math.Max(0, row.Count - 1) * Gap;

    private static IEnumerable<DistrictBuildingLayout> PlaceNorth(
        DistrictBuildingInput[] row,
        PlanRect square
    )
    {
        var cursor = LocationSizer.SnapDown(square.X + (square.Width - Extent(row)) / 2);

        foreach (var building in row)
        {
            var top = square.Y - building.Footprint.Depth;
            yield return DistrictFacing.Place(building, cursor, top, facing: 2);
            cursor += building.Footprint.Width + Gap;
        }
    }

    private static IEnumerable<DistrictBuildingLayout> PlaceFlank(
        DistrictBuildingInput[] column,
        PlanRect square,
        double edgeX,
        int facing
    )
    {
        var cursor = LocationSizer.SnapDown(square.Y + (square.Depth - Extent(column)) / 2);

        foreach (var building in column)
        {
            var left = facing == 1 ? edgeX - building.Footprint.Depth : edgeX;
            yield return DistrictFacing.Place(building, left, cursor, facing);
            cursor += building.Footprint.Width + Gap;
        }
    }
}
