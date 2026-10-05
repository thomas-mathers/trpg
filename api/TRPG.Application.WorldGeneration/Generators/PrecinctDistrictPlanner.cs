using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class PrecinctDistrictPlanner
{
    private const double Gap = 3;
    private const double Margin = 4.5;
    private const double CourtPadding = 4.5;
    private const double MinimumCourtWidth = 19.5;
    private const double MinimumCourtDepth = 16.5;
    private const double MinimumSideDepth = 9;
    private const double GateCorridor = CityGrid.GateCorridor;
    private const double AvenueDepth = 9;
    private const int MaximumCityCenterTopRow = 2;

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
            ? Math.Min(
                MaximumCityCenterTopRow,
                ordered.Count(building => CityCenterTopRow.Contains(building.Type))
            )
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
        var northSplit = Split(north, north.Length / 2);
        var westSplit = Split(west, (west.Length + 1) / 2);
        var eastSplit = Split(east, (east.Length + 1) / 2);
        var northHalfSpan = north.Length >= 2 ? Reach(northSplit) : Extent(north) / 2;
        var courtWidth = CityGrid.SnapUpToOddCells(
            Math.Max(MinimumCourtWidth, 2 * northHalfSpan + CourtPadding)
        );
        var axisOffset =
            CityGrid.SnapUp(
                Math.Max(ReachBefore(westSplit), ReachBefore(eastSplit))
                    + CourtPadding / 2
                    - CityGrid.HalfCell
            ) + CityGrid.HalfCell;
        var courtDepth = CityGrid.SnapUp(
            Math.Max(
                MinimumCourtDepth,
                axisOffset
                    + Math.Max(ReachAfter(westSplit), ReachAfter(eastSplit))
                    + CourtPadding / 2
            )
        );
        var square = new PlanRect(
            CityGrid.SnapUp(Margin + sideDepth),
            CityGrid.SnapUp(Math.Max(AvenueDepth, Margin + topDepth)),
            courtWidth,
            courtDepth
        );
        var sideAxis = square.Y + axisOffset;
        var size = new Footprint(
            Width: square.X + courtWidth + square.X,
            Depth: CityGrid.SnapUpToOddCells(square.Bottom + AvenueDepth)
        );
        var buildings = PlaceNorth(north, northSplit, square)
            .Concat(PlaceFlank(westSplit, sideAxis, square.X, facing: 1))
            .Concat(PlaceFlank(eastSplit, sideAxis, square.Right, facing: 3))
            .ToArray();
        var avenue = new PlanRect(
            square.CenterX - GateCorridor / 2,
            square.Bottom,
            GateCorridor,
            AvenueDepth
        );
        var sideCourts = new[]
        {
            new PlanRect(Margin, square.Y, square.X - Margin, courtDepth),
            new PlanRect(square.Right, square.Y, square.X - Margin, courtDepth),
        };

        return new DistrictPlan(size, buildings, [square, avenue], sideCourts, square, sideAxis);
    }

    private static double Extent(IReadOnlyCollection<DistrictBuildingInput> row) =>
        row.Sum(building => building.Footprint.Width) + Math.Max(0, row.Count - 1) * Gap;

    private static BuildingRuns Split(DistrictBuildingInput[] row, int beforeCount) =>
        new(row.Take(beforeCount).ToArray(), row.Skip(beforeCount).ToArray());

    private static double Reach(BuildingRuns runs) => Math.Max(ReachBefore(runs), ReachAfter(runs));

    private static double ReachBefore(BuildingRuns runs) => Extent(runs.Before) + GateCorridor / 2;

    private static double ReachAfter(BuildingRuns runs) => Extent(runs.After) + GateCorridor / 2;

    private static IEnumerable<DistrictBuildingLayout> PlaceNorth(
        DistrictBuildingInput[] row,
        BuildingRuns runs,
        PlanRect square
    )
    {
        if (row.Length < 2)
        {
            var centred = CityGrid.SnapDown(square.X + (square.Width - Extent(row)) / 2);

            return PlaceNorthRun(row, centred, square);
        }

        var before = square.CenterX - GateCorridor / 2 - Extent(runs.Before);
        var after = square.CenterX + GateCorridor / 2;

        return PlaceNorthRun(runs.Before, before, square)
            .Concat(PlaceNorthRun(runs.After, after, square));
    }

    private static IEnumerable<DistrictBuildingLayout> PlaceNorthRun(
        DistrictBuildingInput[] run,
        double start,
        PlanRect square
    )
    {
        var cursor = start;

        foreach (var building in run)
        {
            var top = square.Y - building.Footprint.Depth;
            yield return DistrictFacing.Place(building, cursor, top, facing: 2);
            cursor += building.Footprint.Width + Gap;
        }
    }

    private static IEnumerable<DistrictBuildingLayout> PlaceFlank(
        BuildingRuns runs,
        double axis,
        double edgeX,
        int facing
    )
    {
        var before = axis - GateCorridor / 2 - Extent(runs.Before);
        var after = axis + GateCorridor / 2;

        return PlaceFlankRun(runs.Before, before, edgeX, facing)
            .Concat(PlaceFlankRun(runs.After, after, edgeX, facing));
    }

    private static IEnumerable<DistrictBuildingLayout> PlaceFlankRun(
        DistrictBuildingInput[] run,
        double start,
        double edgeX,
        int facing
    )
    {
        var cursor = start;

        foreach (var building in run)
        {
            var left = facing == 1 ? edgeX - building.Footprint.Depth : edgeX;
            yield return DistrictFacing.Place(building, left, cursor, facing);
            cursor += building.Footprint.Width + Gap;
        }
    }

    private sealed record BuildingRuns(
        DistrictBuildingInput[] Before,
        DistrictBuildingInput[] After
    );
}
