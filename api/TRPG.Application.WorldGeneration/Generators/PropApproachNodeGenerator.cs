using TRPG.Application.Common.Navigation;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal sealed record PropApproachNodes(
    IReadOnlyList<TravelNode> Nodes,
    IReadOnlyList<PointConnector> Connectors
);

internal static class PropApproachNodeGenerator
{
    internal static PropApproachNodes Generate(
        LocationLayoutContext context,
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<TravelNode> graphNodes
    )
    {
        var nodes = new List<TravelNode>();
        var connectors = new List<PointConnector>();

        foreach (var location in context.Locations)
        {
            var approaches = GenerateForLocation(context, location, props, graphNodes);
            nodes.AddRange(approaches.Nodes);
            connectors.AddRange(approaches.Connectors);
        }

        return new PropApproachNodes(nodes, connectors);
    }

    private static PropApproachNodes GenerateForLocation(
        LocationLayoutContext context,
        Location location,
        IReadOnlyCollection<Prop> props,
        IReadOnlyCollection<TravelNode> graphNodes
    )
    {
        var anchors = props
            .Where(prop => IsAnchor(prop) && prop.LocationId == location.Id)
            .ToArray();
        if (anchors.Length == 0)
        {
            return new PropApproachNodes([], []);
        }

        var localNodes = graphNodes.Where(node => node.LocationId == location.Id).ToArray();
        if (localNodes.Length == 0)
        {
            throw new InvalidOperationException(
                "Every prop approach anchor needs a local travel node."
            );
        }

        var grid = BuildGrid(context, location, props);
        var approaches = anchors.Select(anchor => CreateNode(location, anchor, grid)).ToArray();
        return new PropApproachNodes(
            approaches,
            CreateConnectors(location, approaches, localNodes, grid)
        );
    }

    private static IReadOnlyList<PointConnector> CreateConnectors(
        Location location,
        IReadOnlyList<TravelNode> approaches,
        IReadOnlyList<TravelNode> localNodes,
        NavigationGrid grid
    ) =>
        approaches
            .Select(approach =>
            {
                var target = localNodes.MinBy(node => Distance(approach.Position, node.Position))!;
                return CreateConnector(
                    location,
                    approach,
                    target,
                    grid.FindPath(approach.Position, target.Position)
                );
            })
            .ToArray();

    private static TravelNode CreateNode(Location location, Prop prop, NavigationGrid grid)
    {
        var node = new TravelNode
        {
            WorldId = location.WorldId,
            LocationId = location.Id,
            Position = NearestWalkablePoint(grid, new Point(prop.X, prop.Y)),
        };
        prop.ApproachNodeId = node.Id;
        return node;
    }

    private static bool IsAnchor(Prop prop) =>
        prop
            is Seat
                or Bed { AssignedCreatureId: not null }
                or Workstation { AssignedCreatureId: not null };

    private static NavigationGrid BuildGrid(
        LocationLayoutContext context,
        Location location,
        IReadOnlyCollection<Prop> props
    )
    {
        var obstacles = props
            .Where(prop => prop.LocationId == location.Id && BlocksFloor(prop))
            .Select(BoxOf)
            .Concat(
                context
                    .BuildingsByExterior[location.Id]
                    .Select(building =>
                        OrientedBox.From(
                            new Placement(building.X, building.Y, building.Angle),
                            new Footprint(building.Width, building.Depth)
                        )
                    )
            )
            .ToArray();

        return new NavigationGrid(
            location.Width,
            location.Depth,
            RoomNavigationGrid.CellSize,
            point => obstacles.Any(obstacle => Contains(obstacle, point))
        );
    }

    private static bool BlocksFloor(Prop prop) =>
        prop is not (Sign or Trap or Trigger)
        && PropModelResolver.Resolve(prop)
            is not (
                PropModel.FurnitureRug
                or PropModel.FurnitureChandelier
                or PropModel.FurnitureWallSconce
                or PropModel.FurnitureWallLantern
                or PropModel.FurnitureBanner
            );

    private static Point NearestWalkablePoint(NavigationGrid grid, Point propPosition) =>
        grid.FindNearestFreePoint(propPosition);

    private static PointConnector CreateConnector(
        Location location,
        TravelNode approach,
        TravelNode target,
        IReadOnlyList<Point> path
    ) =>
        new()
        {
            WorldId = location.WorldId,
            LocationId = location.Id,
            OriginNodeId = approach.Id,
            DestinationNodeId = target.Id,
            Bidirectional = true,
            Distance = PathLength(path),
            Waypoints = new Polyline { Points = path.Skip(1).SkipLast(1).ToList() },
        };

    private static OrientedBox BoxOf(Prop prop) =>
        OrientedBox.From(
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );

    private static bool Contains(OrientedBox box, Point point)
    {
        var (sin, cos) = Math.SinCos(box.Angle);
        var offsetX = point.X - box.CenterX;
        var offsetY = point.Y - box.CenterY;
        var localX = (offsetX * cos) + (offsetY * sin);
        var localY = (-offsetX * sin) + (offsetY * cos);

        return Math.Abs(localX) < box.Width / 2 && Math.Abs(localY) < box.Depth / 2;
    }

    private static double PathLength(IReadOnlyList<Point> path) =>
        path.Zip(path.Skip(1)).Sum(pair => Distance(pair.First, pair.Second));

    private static double Distance(Point from, Point to) =>
        Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
}
