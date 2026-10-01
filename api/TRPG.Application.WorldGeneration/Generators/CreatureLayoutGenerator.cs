using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class CreatureLayoutGenerator
{
    public static void Place(CreatureLayoutInput input)
    {
        var context = new CreatureLayoutContext(input);

        foreach (var creature in input.Creatures)
        {
            Apply(creature, ResolveNearAnchor(creature, context), context);
        }
    }

    public static void PlaceAtLocationCenter(CreatureLayoutInput input)
    {
        var context = new CreatureLayoutContext(input);

        foreach (var creature in input.Creatures)
        {
            var location = context.LocationById[creature.LocationId];
            var center = new Placement(location.Width / 2, location.Depth / 2, 0);
            var frame = new Footprint(Width: location.Width, Depth: location.Depth);
            var obstacles = context.ObstaclesAt(location.Id, excludedPropId: null);

            Apply(creature, CreaturePlacementResolver.PlaceAt(frame, obstacles, center), context);
        }
    }

    public static void PlaceAtArrival(CreatureLayoutInput input)
    {
        var context = new CreatureLayoutContext(input);

        foreach (var creature in input.Creatures)
        {
            var location = context.LocationById[creature.LocationId];
            var frame = new Footprint(Width: location.Width, Depth: location.Depth);
            var arrival = creature.PreviousLocationId is { } previousLocationId
                ? context.ArrivalPointFrom(previousLocationId, location.Id)
                : null;
            var preferred = arrival ?? ConnectorPointResolver.ResolveDefaultArrival(frame);
            var obstacles = context.ArrivalObstaclesAt(location.Id);

            Apply(
                creature,
                CreaturePlacementResolver.PlaceAt(frame, obstacles, preferred),
                context
            );
        }
    }

    private static Placement ResolveNearAnchor(Creature creature, CreatureLayoutContext context)
    {
        var location = context.LocationById[creature.LocationId];
        var frame = new Footprint(Width: location.Width, Depth: location.Depth);
        var anchor = CreatureAnchorFinder.Find(creature, context.PropsAt(location.Id));
        var seed = LayoutSeed.From(creature.Id);

        if (anchor is null)
        {
            return CreaturePlacementResolver.PlaceFree(
                frame,
                context.ObstaclesAt(location.Id, excludedPropId: null),
                seed
            );
        }

        var pose = CreatureLayoutContext.PoseOf(anchor.Prop);

        return anchor.IsOccupied
            ? CreaturePlacementResolver.PlaceAt(
                frame,
                context.ObstaclesAt(location.Id, excludedPropId: anchor.Prop.Id),
                pose
            )
            : CreaturePlacementResolver.PlaceNear(
                frame,
                context.ObstaclesAt(location.Id, excludedPropId: null),
                pose,
                seed
            );
    }

    private static void Apply(Creature creature, Placement placement, CreatureLayoutContext context)
    {
        creature.X = placement.X;
        creature.Y = placement.Y;
        creature.Angle = placement.Angle;
        context.Record(creature);
    }
}
