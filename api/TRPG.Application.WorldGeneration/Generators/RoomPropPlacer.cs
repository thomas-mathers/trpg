using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record RoomPlacementResult(
    Footprint Room,
    IReadOnlyList<ConnectorExit> Exits,
    IReadOnlyList<PlacedProp> Props
);

internal static class RoomPropPlacer
{
    internal const int MaximumGrowths = 3;
    internal const double GrowthFactor = 1.1;

    internal static RoomPlacementResult Place(
        Footprint room,
        IReadOnlyCollection<RoomPropInput> props,
        IReadOnlyCollection<ConnectorExitRequest> exitRequests,
        Random random
    )
    {
        var ordered = OrderForPlacement(props);

        for (var growth = 0; growth < MaximumGrowths; growth++)
        {
            var exits = ConnectorPointResolver.ResolveExits(room, exitRequests);
            var session = new RoomPlacementSession(room, exits, random);

            if (ordered.All(session.TryPlace))
            {
                return new RoomPlacementResult(room, exits, session.Placed);
            }

            room = Grow(room);
        }

        return PlaceBestEffort(room, ordered, exitRequests, random);
    }

    private static RoomPlacementResult PlaceBestEffort(
        Footprint room,
        IReadOnlyList<RoomPropInput> ordered,
        IReadOnlyCollection<ConnectorExitRequest> exitRequests,
        Random random
    )
    {
        var exits = ConnectorPointResolver.ResolveExits(room, exitRequests);
        var session = new RoomPlacementSession(room, exits, random);

        foreach (var prop in ordered)
        {
            session.PlaceBestEffort(prop);
        }

        return new RoomPlacementResult(room, exits, session.Placed);
    }

    private static Footprint Grow(Footprint room) =>
        new(
            Width: LocationSizer.SnapUp(room.Width * GrowthFactor),
            Depth: LocationSizer.SnapUp(room.Depth * GrowthFactor)
        );

    private static List<RoomPropInput> OrderForPlacement(
        IReadOnlyCollection<RoomPropInput> props
    ) =>
        props
            .Select(prop => new { Prop = prop, Spec = PropFootprintCatalog.Get(prop.Model) })
            .OrderBy(entry => RulePriority(entry.Spec.Rule))
            .ThenByDescending(entry => entry.Spec.Width * entry.Spec.Depth)
            .ThenBy(entry => entry.Prop.Id)
            .Select(entry => entry.Prop)
            .ToList();

    private static int RulePriority(PropPlacementRule rule) =>
        rule switch
        {
            PropPlacementRule.Corner => 0,
            PropPlacementRule.Wall => 1,
            PropPlacementRule.Center => 2,
            PropPlacementRule.Anchor => 3,
            _ => 4,
        };
}
