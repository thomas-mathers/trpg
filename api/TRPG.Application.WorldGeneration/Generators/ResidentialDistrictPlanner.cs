using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ResidentialDistrictPlanner
{
    internal const double StreetWidth = 7;
    private const double Alley = 3;
    private const double MinimumCourt = 20;
    private const double MinimumBand = 8;
    private const double BlockJitter = 20;
    private const double MinimumWeight = 0.8;
    private const double WeightRange = 0.5;
    private const int StripCount = 4;

    internal static DistrictPlan Plan(
        IReadOnlyCollection<DistrictBuildingInput> buildings,
        Random random
    )
    {
        var measure = BlockMeasure.Of(buildings);
        var grid = BlockGrid.Create(buildings.Count, measure, random);
        var queue = new Queue<DistrictBuildingInput>(buildings.OrderBy(building => building.Id));
        var quota = (int)Math.Ceiling(buildings.Count / (double)Math.Max(1, grid.BlockCount - 1));
        var placed = new List<DistrictBuildingLayout>();
        var courts = new List<PlanRect>();
        var square = default(PlanRect);

        foreach (var cell in grid.Cells(StreetWidth))
        {
            if (cell.Index == grid.SquareIndex)
            {
                square = cell.Block;
                continue;
            }

            placed.AddRange(FillBlock(cell.Block, queue, quota, measure.Band, random));
            courts.Add(Court(cell.Block, measure.Band));
        }

        return new DistrictPlan(
            grid.Size(StreetWidth),
            placed,
            grid.Streets(StreetWidth),
            courts,
            square
        );
    }

    private static PlanRect Court(PlanRect block, double band) =>
        new(block.X + band, block.Y + band, block.Width - 2 * band, block.Depth - 2 * band);

    private static IEnumerable<DistrictBuildingLayout> FillBlock(
        PlanRect block,
        Queue<DistrictBuildingInput> queue,
        int quota,
        double band,
        Random random
    )
    {
        var strips = Strips(block, band);
        var rows = strips.Select(_ => new List<DistrictBuildingInput>()).ToArray();
        var next = 0;

        for (var taken = 0; taken < quota && queue.Count > 0; taken++)
        {
            var index = FindStrip(strips, rows, queue.Peek(), next);

            if (index < 0)
            {
                break;
            }

            rows[index].Add(queue.Dequeue());
            next = (index + 1) % StripCount;
        }

        return strips.SelectMany((strip, index) => PlaceStrip(strip, rows[index], random));
    }

    private static Strip[] Strips(PlanRect block, double band)
    {
        var sideTop = block.Y + band + Alley;
        var sideDepth = block.Depth - 2 * band - 2 * Alley;

        return
        [
            new Strip(new PlanRect(block.X, block.Y, block.Width, band), Facing: 0),
            new Strip(new PlanRect(block.X, block.Bottom - band, block.Width, band), Facing: 2),
            new Strip(new PlanRect(block.X, sideTop, band, sideDepth), Facing: 3),
            new Strip(new PlanRect(block.Right - band, sideTop, band, sideDepth), Facing: 1),
        ];
    }

    private static int FindStrip(
        Strip[] strips,
        List<DistrictBuildingInput>[] rows,
        DistrictBuildingInput candidate,
        int start
    )
    {
        for (var offset = 0; offset < StripCount; offset++)
        {
            var index = (start + offset) % StripCount;
            var used = rows[index].Sum(building => building.Footprint.Width + Alley);

            if (used + candidate.Footprint.Width <= strips[index].Length + 1e-9)
            {
                return index;
            }
        }

        return -1;
    }

    private static IEnumerable<DistrictBuildingLayout> PlaceStrip(
        Strip strip,
        List<DistrictBuildingInput> row,
        Random random
    )
    {
        if (row.Count == 0)
        {
            yield break;
        }

        var leftover =
            strip.Length - row.Sum(building => building.Footprint.Width) - (row.Count - 1) * Alley;
        var weights = Enumerable
            .Range(0, row.Count + 1)
            .Select(_ => MinimumWeight + random.NextDouble() * WeightRange)
            .ToArray();
        var share = leftover / weights.Sum();
        var cursor = share * weights[0];

        for (var index = 0; index < row.Count; index++)
        {
            var building = row[index];
            yield return strip.Place(building, LocationSizer.SnapDown(cursor));
            cursor += building.Footprint.Width + Alley + share * weights[index + 1];
        }
    }

    private record Strip(PlanRect Area, int Facing)
    {
        internal double Length => Facing % 2 == 1 ? Area.Depth : Area.Width;

        internal DistrictBuildingLayout Place(DistrictBuildingInput building, double along)
        {
            var rotated = DistrictFacing.Rotated(building.Footprint, Facing);

            return Facing switch
            {
                0 => DistrictFacing.Place(building, Area.X + along, Area.Y, Facing),
                2 => DistrictFacing.Place(
                    building,
                    Area.X + along,
                    Area.Bottom - rotated.Depth,
                    Facing
                ),
                3 => DistrictFacing.Place(building, Area.X, Area.Y + along, Facing),
                _ => DistrictFacing.Place(
                    building,
                    Area.Right - rotated.Width,
                    Area.Y + along,
                    Facing
                ),
            };
        }
    }

    private record BlockMeasure(double Band, double MinimumSide, int GuaranteedCapacity)
    {
        internal static BlockMeasure Of(IReadOnlyCollection<DistrictBuildingInput> buildings)
        {
            var band = LocationSizer.SnapUp(
                Math.Max(
                    MinimumBand,
                    buildings.Select(b => b.Footprint.Depth).DefaultIfEmpty(0).Max()
                )
            );
            var frontage = buildings.Select(b => b.Footprint.Width).DefaultIfEmpty(0).Max();
            var side = LocationSizer.SnapUp(Math.Max(2 * band + MinimumCourt, frontage));
            var sideStrip = side - 2 * band - 2 * Alley;
            var capacity = 2 * (FitCount(side, frontage) + FitCount(sideStrip, frontage));

            return new BlockMeasure(band, side, capacity);
        }

        private static int FitCount(double length, double frontage) =>
            Math.Max(0, (int)Math.Floor((length + Alley) / (frontage + Alley)));
    }

    private record BlockCell(int Index, PlanRect Block);

    private record BlockGrid(double[] ColumnWidths, double[] RowDepths, int SquareIndex)
    {
        internal int BlockCount => ColumnWidths.Length * RowDepths.Length;

        internal static BlockGrid Create(int buildingCount, BlockMeasure measure, Random random)
        {
            var blocks = 1 + (int)Math.Ceiling(buildingCount / (double)measure.GuaranteedCapacity);
            var columns = (int)Math.Ceiling(Math.Sqrt(blocks));
            var rows = (int)Math.Ceiling(blocks / (double)columns);
            var widths = RandomSides(columns, measure, random);
            var depths = RandomSides(rows, measure, random);

            return new BlockGrid(widths, depths, random.Next(columns * rows));
        }

        internal IEnumerable<BlockCell> Cells(double street)
        {
            var y = street;

            for (var row = 0; row < RowDepths.Length; row++)
            {
                var x = street;

                for (var column = 0; column < ColumnWidths.Length; column++)
                {
                    var block = new PlanRect(x, y, ColumnWidths[column], RowDepths[row]);
                    yield return new BlockCell(row * ColumnWidths.Length + column, block);
                    x += ColumnWidths[column] + street;
                }

                y += RowDepths[row] + street;
            }
        }

        internal Footprint Size(double street) =>
            new(
                ColumnWidths.Sum() + street * (ColumnWidths.Length + 1),
                RowDepths.Sum() + street * (RowDepths.Length + 1)
            );

        internal IReadOnlyList<PlanRect> Streets(double street)
        {
            var size = Size(street);
            var streets = new List<PlanRect>();
            var y = 0.0;

            foreach (var depth in RowDepths.Append(0))
            {
                streets.Add(new PlanRect(0, y, size.Width, street));
                y += depth + street;
            }

            var x = 0.0;

            foreach (var width in ColumnWidths.Append(0))
            {
                streets.Add(new PlanRect(x, 0, street, size.Depth));
                x += width + street;
            }

            return streets;
        }

        private static double[] RandomSides(int count, BlockMeasure measure, Random random) =>
            Enumerable
                .Range(0, count)
                .Select(_ =>
                    LocationSizer.SnapUp(measure.MinimumSide + random.NextDouble() * BlockJitter)
                )
                .ToArray();
    }
}
