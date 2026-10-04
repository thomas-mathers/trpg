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
    private const double Clearance = 0.35;
    private const double ApproachWidth = 2;
    private const double ApproachDepth = 1.5;

    private static readonly double[] FrontFractions = [-0.3, 0.3, -0.4, 0.4];
    private static readonly double[] BenchFractions = [0.2, 0.5, 0.8, 0.35, 0.65];
    private static readonly double[] BoardFractions = [0.5, 0.25, 0.75, 0.1, 0.9];

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

        PlaceOptional(decor, occupied, Centerpiece(type, plan.Square));
        PlaceOptional(decor, occupied, BoardCandidates(plan.Square));

        var fronts = FrontBoxes(plan, buildings);

        return new OutdoorFurnishing(PlaceSeats(seats, plan.Square, occupied, fronts), decor);
    }

    private static List<OrientedBox> Blocked(
        DistrictPlan plan,
        IReadOnlyCollection<DistrictBuildingInput> buildings
    ) => FrontBoxes(plan, buildings).Concat(plan.Buildings.Select(Approach)).ToList();

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

    private static OrientedBox Approach(DistrictBuildingLayout layout)
    {
        var angle = layout.Placement.Angle;
        var center = new Placement(
            layout.DoorPoint.X + ApproachDepth / 2 * Math.Sin(angle),
            layout.DoorPoint.Y - ApproachDepth / 2 * Math.Cos(angle),
            angle
        );

        return OrientedBox.From(center, new Footprint(ApproachWidth, ApproachDepth));
    }

    private static DistrictDecor[] Centerpiece(DistrictType type, PlanRect square) =>
        [Decor(Centerpieces[type], square.CenterX, square.CenterY, angle: 0)];

    private static DistrictDecor[] BoardCandidates(PlanRect square)
    {
        var footprint = PropFootprintCatalog.Get(PropModel.FurnitureNoticeBoard).Footprint;

        return BoardFractions
            .Select(fraction =>
                Decor(
                    PropModel.FurnitureNoticeBoard,
                    square.X + fraction * square.Width,
                    square.Y + SquareInset + footprint.Depth / 2,
                    angle: Math.PI
                )
            )
            .ToArray();
    }

    private static DistrictDecor Decor(PropModel model, double x, double y, double angle) =>
        new(model, new Placement(x, y, angle), PropFootprintCatalog.Get(model).Footprint);

    private static void PlaceOptional(
        List<DistrictDecor> decor,
        List<OrientedBox> occupied,
        DistrictDecor[] candidates
    )
    {
        var fit = candidates.FirstOrDefault(candidate =>
            IsFree(candidate.Placement, candidate.Footprint, occupied)
        );

        if (fit is null)
        {
            return;
        }

        decor.Add(fit);
        occupied.Add(OrientedBox.From(fit.Placement, fit.Footprint));
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
                    .FirstOrDefault(slot => IsFree(slot, seat.Footprint, occupied))
                ?? throw new InvalidOperationException(
                    $"The square has no free bench slot for seat {seat.Id}."
                );

            occupied.Add(OrientedBox.From(placement, seat.Footprint));
            layouts.Add(new DistrictSeatLayout(seat.Id, placement));
        }

        return layouts;
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
        IReadOnlyCollection<OrientedBox> occupied
    )
    {
        var box = OrientedBox.From(placement, footprint).Inflated(Clearance / 2);

        return occupied.All(other => !box.Overlaps(other));
    }
}
