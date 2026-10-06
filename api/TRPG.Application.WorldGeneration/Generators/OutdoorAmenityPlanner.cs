using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record OutdoorAmenityInput(
    DistrictPlan Plan,
    IReadOnlyDictionary<Guid, BuildingType> BuildingTypes,
    IReadOnlyList<DistrictDecor> Existing,
    IReadOnlyList<OrientedBox> Blocked,
    RoadNetwork Roads
);

internal static class OutdoorAmenityPlanner
{
    private const double RoadMargin = 0.75;
    private const double LanternSpacing = 27;
    private const double LanternSeparation = 24;

    internal static IReadOnlyList<DistrictDecor> Place(DistrictType type, OutdoorAmenityInput input)
    {
        var state = new PlacementState(input);

        PlaceCourts(type, input.Plan, state);
        PlaceSquare(type, input.Plan, input.Existing, state);
        PlaceFronts(type, input.Plan, input.BuildingTypes, state);
        PlaceRoadLanterns(input.Plan, state);

        return state.Placed.ToArray();
    }

    private static void PlaceCourts(DistrictType type, DistrictPlan plan, PlacementState state)
    {
        if (type == DistrictType.Residential)
            PlaceResidentialCourts(plan.Courts, state);
        if (type == DistrictType.HolySite)
            PlaceTempleCourts(plan.Courts, state);
        if (type == DistrictType.Governmental && plan.Courts.Count > 1)
        {
            var court = plan.Courts[^1];
            state.TryPlace(PropModel.FurnitureTree, court.CenterX, court.CenterY, 0, court);
        }
        if (type == DistrictType.CityEntrance && plan.Courts.Count > 1)
            PlaceEntranceCourts(plan.Courts, state);
    }

    private static void PlaceResidentialCourts(IReadOnlyList<PlanRect> courts, PlacementState state)
    {
        foreach (var court in courts)
        {
            state.TryPlace(PropModel.FurnitureTree, court.CenterX, court.CenterY, 0, court);
            state.TryPlace(
                PropModel.FurnitureShrub,
                court.CenterX + court.Width * 0.28,
                court.CenterY + court.Depth * 0.25,
                0,
                court
            );
        }
    }

    private static void PlaceTempleCourts(IReadOnlyList<PlanRect> courts, PlacementState state)
    {
        foreach (var court in courts)
        {
            state.TryPlace(
                PropModel.FurnitureFlowerBed,
                court.CenterX,
                court.Y + CityGrid.HalfCell,
                0,
                court
            );
            state.TryPlace(PropModel.FurnitureTree, court.CenterX, court.Bottom - 3, 0, court);
        }
    }

    private static void PlaceEntranceCourts(IReadOnlyList<PlanRect> courts, PlacementState state)
    {
        var west = courts[0];
        var east = courts[^1];
        state.TryPlace(
            PropModel.FurnitureHitchingRail,
            west.CenterX,
            west.Bottom - 3,
            Math.PI / 2,
            west
        );
        state.TryPlace(
            PropModel.FurnitureTrough,
            east.CenterX,
            east.Bottom - 3,
            3 * Math.PI / 2,
            east
        );
    }

    private static void PlaceSquare(
        DistrictType type,
        DistrictPlan plan,
        IReadOnlyList<DistrictDecor> existing,
        PlacementState state
    )
    {
        var square = plan.Square;
        if (type == DistrictType.CityCenter)
        {
            PlaceMarket(square, state);
            PlaceFountainSeats(square, existing, state);
        }
        else if (type == DistrictType.Governmental)
        {
            PlaceBanners(square, state);
        }
        else if (type == DistrictType.Residential)
        {
            PlaceSquareTrees(square, state);
        }
        else if (type == DistrictType.Encampment)
        {
            PlaceTrainingGear(square, state);
        }
        else if (type == DistrictType.CityEntrance)
        {
            state.TryPlace(
                PropModel.FurnitureCart,
                square.X + square.Width * 0.8,
                square.Y + square.Depth * 0.72,
                3 * Math.PI / 2,
                square
            );
        }
    }

    private static void PlaceBanners(PlanRect square, PlacementState state)
    {
        foreach (var fraction in new[] { 0.35, 0.65 })
        {
            state.TryPlace(
                PropModel.FurnitureBanner,
                square.X + square.Width * fraction,
                square.Y + square.Depth * 0.27,
                Math.PI,
                square
            );
        }
    }

    private static void PlaceSquareTrees(PlanRect square, PlacementState state)
    {
        foreach (var fraction in new[] { 0.3, 0.7 })
        {
            state.TryPlace(
                PropModel.FurnitureTree,
                square.X + square.Width * 0.77,
                square.Y + square.Depth * fraction,
                0,
                square
            );
        }
    }

    private static void PlaceTrainingGear(PlanRect square, PlacementState state)
    {
        state.TryPlace(
            PropModel.FurnitureTrainingDummy,
            square.X + square.Width * 0.18,
            square.Y + square.Depth * 0.25,
            Math.PI / 2,
            square
        );
        state.TryPlace(
            PropModel.ContainerWeaponRack,
            square.X + square.Width * 0.18,
            square.Y + square.Depth * 0.43,
            Math.PI / 2,
            square
        );
    }

    private static void PlaceMarket(PlanRect square, PlacementState state)
    {
        var x = square.Right - Math.Min(14, square.Width * 0.22);
        foreach (var fraction in new[] { 0.21, 0.37, 0.53 })
        {
            state.TryPlace(
                PropModel.FurnitureStall,
                x,
                square.Y + square.Depth * fraction,
                Math.PI / 2,
                square
            );
        }
    }

    private static void PlaceFountainSeats(
        PlanRect square,
        IReadOnlyList<DistrictDecor> existing,
        PlacementState state
    )
    {
        var fountain = existing.FirstOrDefault(item => item.Model == PropModel.FurnitureFountain);
        if (fountain is null)
            return;

        foreach (
            var (dx, dy, angle) in new[]
            {
                (-5.5, 4.5, Math.PI / 4),
                (5.5, 4.5, 7 * Math.PI / 4),
                (0.0, -5.5, Math.PI),
            }
        )
        {
            state.TryPlace(
                PropModel.SeatBench,
                fountain.Placement.X + dx,
                fountain.Placement.Y + dy,
                angle,
                square
            );
        }
    }

    private static void PlaceFronts(
        DistrictType type,
        DistrictPlan plan,
        IReadOnlyDictionary<Guid, BuildingType> buildingTypes,
        PlacementState state
    )
    {
        var district = new PlanRect(0, 0, plan.Size.Width, plan.Size.Depth);
        foreach (var building in plan.Buildings)
        {
            var buildingType = buildingTypes[building.Id];
            if (buildingType == BuildingType.Apothecary)
            {
                PlaceHerbs(building, district, state);
            }

            var model = FrontageModel(buildingType);
            if (model is not null)
            {
                var point = FrontPoint(building, 4.5, 2.7);
                state.TryPlace(model.Value, point.X, point.Y, building.Placement.Angle, district);
            }

            if (type != DistrictType.Residential && buildingType != BuildingType.House)
            {
                var point = FrontPoint(building, -3.2, 0.08);
                state.TryPlaceWallMounted(
                    PropModel.FurnitureWallLantern,
                    point.X,
                    point.Y,
                    building.Placement.Angle,
                    district
                );
            }
        }
    }

    private static void PlaceHerbs(
        DistrictBuildingLayout building,
        PlanRect district,
        PlacementState state
    )
    {
        foreach (var along in new[] { -5.0, -3.0, 3.0, 5.0 })
        {
            var tub = FrontPoint(building, along, 2.7);
            state.TryPlace(
                PropModel.FurnitureHerbTub,
                tub.X,
                tub.Y,
                building.Placement.Angle,
                district
            );
        }
    }

    private static PropModel? FrontageModel(BuildingType type) =>
        type switch
        {
            BuildingType.Bakery => PropModel.FurnitureFlourSacks,
            BuildingType.Carpenter => PropModel.FurnitureLumberStack,
            BuildingType.GeneralGoods => PropModel.FurnitureCart,
            BuildingType.Inn => PropModel.FurnitureHitchingRail,
            BuildingType.Stable => PropModel.FurnitureTrough,
            BuildingType.Temple => PropModel.FurnitureFlowerBed,
            BuildingType.Barracks => PropModel.FurnitureTrainingDummy,
            _ => null,
        };

    private static PlanarPoint FrontPoint(
        DistrictBuildingLayout building,
        double along,
        double ahead
    )
    {
        var (sin, cos) = Math.SinCos(building.Placement.Angle);
        return new PlanarPoint(
            building.DoorPoint.X + along * cos + ahead * sin,
            building.DoorPoint.Y + along * sin - ahead * cos
        );
    }

    private static void PlaceRoadLanterns(DistrictPlan plan, PlacementState state)
    {
        var district = new PlanRect(0, 0, plan.Size.Width, plan.Size.Depth);
        var index = 0;
        foreach (var road in state.Roads)
        {
            var dx = road.End.X - road.Start.X;
            var dy = road.End.Y - road.Start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 12)
                continue;

            var offset = road.Width / 2 + 2.25;
            for (var distance = 12.0; distance < length - 4; distance += LanternSpacing)
            {
                var side = (index++ & 1) == 0 ? 1 : -1;
                var x = road.Start.X + dx * distance / length;
                var y = road.Start.Y + dy * distance / length;
                if (
                    !state.TryPlace(
                        PropModel.FurnitureStreetLantern,
                        x - side * dy / length * offset,
                        y + side * dx / length * offset,
                        0,
                        district
                    )
                )
                {
                    state.TryPlace(
                        PropModel.FurnitureStreetLantern,
                        x + side * dy / length * offset,
                        y - side * dx / length * offset,
                        0,
                        district
                    );
                }
            }
        }
    }

    private static IReadOnlyList<RoadSegment> RoadSegments(RoadNetwork network)
    {
        var nodes = network.Nodes.ToDictionary(node => node.Id);
        return network
            .Edges.SelectMany(edge =>
            {
                var points = new[] { new Point(nodes[edge.FromNodeId].X, nodes[edge.FromNodeId].Y) }
                    .Concat(edge.Waypoints.Points)
                    .Append(new Point(nodes[edge.ToNodeId].X, nodes[edge.ToNodeId].Y))
                    .ToArray();
                return points
                    .Skip(1)
                    .Select(
                        (point, index) =>
                            new RoadSegment(points[index], point, RoadClassWidths.Of(edge.Class))
                    );
            })
            .ToArray();
    }

    private static double SegmentDistance(double x, double y, RoadSegment segment)
    {
        var dx = segment.End.X - segment.Start.X;
        var dy = segment.End.Y - segment.Start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0)
            return Math.Sqrt(Math.Pow(x - segment.Start.X, 2) + Math.Pow(y - segment.Start.Y, 2));
        var fraction = Math.Clamp(
            ((x - segment.Start.X) * dx + (y - segment.Start.Y) * dy) / lengthSquared,
            0,
            1
        );
        var remainingX = x - segment.Start.X - fraction * dx;
        var remainingY = y - segment.Start.Y - fraction * dy;
        return Math.Sqrt(remainingX * remainingX + remainingY * remainingY);
    }

    private sealed class PlacementState(OutdoorAmenityInput input)
    {
        private readonly List<OrientedBox> _occupied = input
            .Blocked.Concat(
                input.Existing.Select(item => CityGrid.CellBox(item.Placement, item.Footprint))
            )
            .ToList();

        internal List<DistrictDecor> Placed { get; } = [];
        internal IReadOnlyList<RoadSegment> Roads { get; } = RoadSegments(input.Roads);

        internal bool TryPlace(PropModel model, double x, double y, double angle, PlanRect zone) =>
            TryPlace(model, new Placement(x, y, angle), zone);

        internal bool TryPlaceWallMounted(
            PropModel model,
            double x,
            double y,
            double angle,
            PlanRect zone
        )
        {
            var footprint = PropFootprintCatalog.Get(model).Footprint;
            var placement = new Placement(x, y, angle);
            if (!OrientedBox.From(placement, footprint).IsInside(zone.Right, zone.Bottom))
                return false;

            Placed.Add(new DistrictDecor(model, placement, footprint));
            return true;
        }

        private bool TryPlace(PropModel model, Placement requested, PlanRect zone)
        {
            var footprint = PropFootprintCatalog.Get(model).Footprint;
            var placement = CityGrid.SnapCentre(requested, footprint);
            var box = CityGrid.CellBox(placement, footprint);
            if (!FitsZone(box, zone) || _occupied.Any(other => box.Overlaps(other)))
                return false;

            if (model == PropModel.FurnitureStreetLantern && NearStreetLantern(placement))
                return false;

            var radius = Math.Sqrt(box.Width * box.Width + box.Depth * box.Depth) / 2;
            if (
                Roads.Any(road =>
                    SegmentDistance(placement.X, placement.Y, road)
                    < road.Width / 2 + radius + RoadMargin
                )
            )
                return false;

            Placed.Add(new DistrictDecor(model, placement, footprint));
            _occupied.Add(box);
            return true;
        }

        private bool NearStreetLantern(Placement placement) =>
            Placed.Any(item =>
                item.Model == PropModel.FurnitureStreetLantern
                && Math.Pow(item.Placement.X - placement.X, 2)
                    + Math.Pow(item.Placement.Y - placement.Y, 2)
                    < LanternSeparation * LanternSeparation
            );

        private static bool FitsZone(OrientedBox box, PlanRect zone) =>
            box.CenterX - box.Width / 2 >= zone.X
            && box.CenterX + box.Width / 2 <= zone.Right
            && box.CenterY - box.Depth / 2 >= zone.Y
            && box.CenterY + box.Depth / 2 <= zone.Bottom;
    }

    private sealed record RoadSegment(Point Start, Point End, double Width);
}
