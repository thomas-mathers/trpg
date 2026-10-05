using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DistrictDecor(PropModel Model, Placement Placement, Footprint Footprint);

internal record OutdoorFurnishing(
    IReadOnlyList<DistrictSeatLayout> Seats,
    IReadOnlyList<DistrictDecor> Decor
);

internal static class OutdoorFurnisher
{
    private const double SquareInset = 1;
    private const double ApproachWidth = CityGrid.CellSize;
    private const double ApproachDepth = CityGrid.CellSize;

    private static readonly double[] FrontFractions = [-0.3, 0.3, -0.4, 0.4];
    private static readonly double[] BenchFractions = [0.2, 0.5, 0.8, 0.35, 0.65];
    private static readonly double[] BoardFractions = [0.2, 0.8];

    private static readonly IReadOnlyDictionary<DistrictType, PropModel> Centerpieces =
        new Dictionary<DistrictType, PropModel>
        {
            [DistrictType.Residential] = PropModel.FurnitureWell,
            [DistrictType.CityCenter] = PropModel.FurnitureFountain,
            [DistrictType.Encampment] = PropModel.FurnitureFirePit,
            [DistrictType.Scientific] = PropModel.FurnitureStatue,
            [DistrictType.Governmental] = PropModel.FurnitureMonument,
            [DistrictType.HolySite] = PropModel.FurnitureShrine,
            [DistrictType.CityEntrance] = PropModel.FurnitureWaystone,
        };

    internal static OutdoorFurnishing Furnish(
        DistrictType type,
        DistrictPlan plan,
        IReadOnlyCollection<DistrictBuildingInput> buildings,
        IReadOnlyCollection<DistrictSeatInput> seats
    )
    {
        var occupied = Blocked(plan, buildings);
        var decor = new List<DistrictDecor>();
        PlaceOptional(decor, occupied, plan.Square, Centerpiece(type, plan));
        PlaceOptional(
            decor,
            occupied,
            plan.Square,
            BoardCandidates(plan.Square)
                .Where(candidate =>
                    type is not (DistrictType.Scientific or DistrictType.Governmental)
                    || decor.All(centerpiece =>
                        DistanceSquared(candidate.Placement, centerpiece.Placement) >= 36
                    )
                )
                .ToArray()
        );

        var fronts = FrontBoxes(plan, buildings);
        var placedSeats = PlaceSeats(seats, plan.Square, occupied, fronts);

        return new OutdoorFurnishing(placedSeats, decor);
    }

    internal static List<OrientedBox> Blocked(
        DistrictPlan plan,
        IReadOnlyCollection<DistrictBuildingInput> buildings
    ) =>
        FrontBoxes(plan, buildings)
            .Concat(plan.Buildings.Select(Approach))
            .Concat(GateAxes(plan))
            .ToList();

    private static OrientedBox[] GateAxes(DistrictPlan plan) =>
        [
            new OrientedBox(
                plan.Size.Width / 2,
                plan.Size.Depth / 2,
                CityGrid.GateCorridor,
                plan.Size.Depth,
                Angle: 0
            ),
            new OrientedBox(
                plan.Size.Width / 2,
                plan.SideAxis,
                plan.Size.Width,
                CityGrid.GateCorridor,
                Angle: 0
            ),
        ];

    private static OrientedBox[] FrontBoxes(
        DistrictPlan plan,
        IReadOnlyCollection<DistrictBuildingInput> buildings
    )
    {
        var footprintById = buildings.ToDictionary(
            building => building.Id,
            building => building.Footprint
        );

        return plan
            .Buildings.Select(layout =>
                OrientedBox.From(layout.Placement, footprintById[layout.Id])
            )
            .ToArray();
    }

    internal static OrientedBox Approach(DistrictBuildingLayout layout)
    {
        var angle = layout.Placement.Angle;
        var center = new Placement(
            layout.DoorPoint.X + ApproachDepth / 2 * Math.Sin(angle),
            layout.DoorPoint.Y - ApproachDepth / 2 * Math.Cos(angle),
            angle
        );

        return OrientedBox.From(center, new Footprint(ApproachWidth, ApproachDepth));
    }

    private static DistrictDecor[] Centerpiece(DistrictType type, DistrictPlan plan) =>
        QuadrantCentres(plan)
            .Select(centre => Decor(Centerpieces[type], centre.X, centre.Y, angle: 0))
            .ToArray();

    private static IEnumerable<PlanarPoint> QuadrantCentres(DistrictPlan plan)
    {
        var square = plan.Square;
        var columns = AxisSpans(square.X, square.Right, plan.Size.Width / 2);
        var rows = AxisSpans(square.Y, square.Bottom, plan.SideAxis);

        return rows.SelectMany(row =>
            columns.Select(column => new PlanarPoint(
                (column.Start + column.End) / 2,
                (row.Start + row.End) / 2
            ))
        );
    }

    private static AxisSpan[] AxisSpans(double low, double high, double axis)
    {
        var halfCorridor = CityGrid.GateCorridor / 2;
        var before = new AxisSpan(low, Math.Min(high, axis - halfCorridor));
        var after = new AxisSpan(Math.Max(low, axis + halfCorridor), high);

        return new[] { before, after }.Where(span => span.End > span.Start).ToArray();
    }

    private sealed record AxisSpan(double Start, double End);

    private static DistrictDecor[] BoardCandidates(PlanRect square)
    {
        var footprint = PropFootprintCatalog.Get(PropModel.FurnitureNoticeBoard).Footprint;

        return BoardFractions
            .SelectMany(fraction =>
                new[]
                {
                    Decor(
                        PropModel.FurnitureNoticeBoard,
                        square.X + SquareInset + footprint.Depth / 2,
                        square.Y + fraction * square.Depth,
                        angle: Math.PI / 2
                    ),
                    Decor(
                        PropModel.FurnitureNoticeBoard,
                        square.Right - SquareInset - footprint.Depth / 2,
                        square.Y + fraction * square.Depth,
                        angle: 3 * Math.PI / 2
                    ),
                }
            )
            .ToArray();
    }

    private static double DistanceSquared(Placement first, Placement second) =>
        Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2);

    private static DistrictDecor Decor(PropModel model, double x, double y, double angle)
    {
        var footprint = PropFootprintCatalog.Get(model).Footprint;

        return new DistrictDecor(
            model,
            CityGrid.SnapCentre(new Placement(x, y, angle), footprint),
            footprint
        );
    }

    private static void PlaceOptional(
        List<DistrictDecor> decor,
        List<OrientedBox> occupied,
        PlanRect bounds,
        DistrictDecor[] candidates
    )
    {
        var fit = candidates.FirstOrDefault(candidate =>
            IsFree(candidate.Placement, candidate.Footprint, bounds, occupied)
        );

        if (fit is null)
        {
            return;
        }

        decor.Add(fit);
        occupied.Add(CityGrid.CellBox(fit.Placement, fit.Footprint));
    }

    private static List<DistrictSeatLayout> PlaceSeats(
        IReadOnlyCollection<DistrictSeatInput> seats,
        PlanRect square,
        List<OrientedBox> occupied,
        IReadOnlyCollection<OrientedBox> buildings
    )
    {
        var layouts = new List<DistrictSeatLayout>();

        foreach (var seat in seats.OrderBy(seat => seat.Id))
        {
            var placement =
                FrontSlots(square, buildings, seat.Footprint)
                    .Concat(BenchSlots(square, seat.Footprint))
                    .Concat(EdgeScanSlots(square, seat.Footprint))
                    .Select(slot => CityGrid.SnapCentre(slot, seat.Footprint))
                    .FirstOrDefault(slot => IsFree(slot, seat.Footprint, square, occupied))
                ?? throw new InvalidOperationException(
                    $"The square has no free bench slot for seat {seat.Id}."
                );

            occupied.Add(CityGrid.CellBox(placement, seat.Footprint));
            layouts.Add(new DistrictSeatLayout(seat.Id, placement));
        }

        return layouts;
    }

    private static IEnumerable<Placement> EdgeScanSlots(PlanRect square, Footprint footprint)
    {
        var inset = SquareInset + footprint.Depth / 2;
        var columns = (int)Math.Round(square.Width / CityGrid.CellSize);
        var rows = (int)Math.Round(square.Depth / CityGrid.CellSize);

        var sides = Enumerable
            .Range(0, rows)
            .Select(row => square.Y + (row + 0.5) * CityGrid.CellSize)
            .SelectMany(y =>
                new[]
                {
                    new Placement(square.X + inset, y, Math.PI / 2),
                    new Placement(square.Right - inset, y, 3 * Math.PI / 2),
                }
            );
        var ends = Enumerable
            .Range(0, columns)
            .Select(column => square.X + (column + 0.5) * CityGrid.CellSize)
            .SelectMany(x =>
                new[]
                {
                    new Placement(x, square.Y + inset, Math.PI),
                    new Placement(x, square.Bottom - inset, 0),
                }
            );

        return sides.Concat(ends);
    }

    private static IEnumerable<Placement> BenchSlots(PlanRect square, Footprint footprint) =>
        BenchFractions.SelectMany(fraction =>
            new[]
            {
                new Placement(
                    square.X + SquareInset + footprint.Depth / 2,
                    square.Y + fraction * square.Depth,
                    Math.PI / 2
                ),
                new Placement(
                    square.Right - SquareInset - footprint.Depth / 2,
                    square.Y + fraction * square.Depth,
                    3 * Math.PI / 2
                ),
            }
        );

    private static IEnumerable<Placement> FrontSlots(
        PlanRect square,
        IReadOnlyCollection<OrientedBox> buildings,
        Footprint footprint
    ) =>
        FrontFractions
            .SelectMany(fraction =>
                buildings.Select(building =>
                {
                    var (sin, cos) = Math.SinCos(building.Angle);
                    var ahead = building.Depth / 2 + SquareInset + footprint.Depth / 2;
                    var along = fraction * building.Width;

                    return new Placement(
                        building.CenterX + ahead * sin + along * cos,
                        building.CenterY - ahead * cos + along * sin,
                        building.Angle
                    );
                })
            )
            .Where(slot => square.Contains(new PlanarPoint(slot.X, slot.Y)));

    private static bool IsFree(
        Placement placement,
        Footprint footprint,
        PlanRect bounds,
        IReadOnlyCollection<OrientedBox> occupied
    )
    {
        var box = CityGrid.CellBox(placement, footprint);
        var inside =
            box.CenterX - box.Width / 2 >= bounds.X - 1e-6
            && box.CenterX + box.Width / 2 <= bounds.Right + 1e-6
            && box.CenterY - box.Depth / 2 >= bounds.Y - 1e-6
            && box.CenterY + box.Depth / 2 <= bounds.Bottom + 1e-6;

        return inside && occupied.All(other => !box.Overlaps(other));
    }
}
