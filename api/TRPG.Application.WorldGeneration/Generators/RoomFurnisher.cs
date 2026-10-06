using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record FurnishedRoom(IReadOnlyList<PlacedProp> Bound, IReadOnlyList<RecipeItem> Decor);

internal static class RoomFurnisher
{
    private const double Clearance = 0.12;

    internal static RoomRect KeepOut(ConnectorExit exit)
    {
        var box = ExitKeepOut.Of(exit);

        return new RoomRect(
            box.CenterX - box.Width / 2,
            box.CenterY - box.Depth / 2,
            box.Width,
            box.Depth
        );
    }

    internal static FurnishedRoom Furnish(
        Footprint room,
        RoomRecipe recipe,
        IReadOnlyCollection<RoomPropInput> props,
        IReadOnlyCollection<RoomRect> blocked
    )
    {
        var items = recipe.Expand(room).Where(item => item.Bounds.IsInside(room)).ToArray();
        var decor = items
            .Where(item =>
                item.IsOverlay && (item.IsCeilingMounted || IsFree(item.Bounds, blocked))
            )
            .ToList();
        var unplaced = props.ToList();
        var bound = new List<PlacedProp>();
        var obstacles = blocked.ToList();

        foreach (var item in items.Where(item => !item.IsOverlay && !item.IsWallMounted))
        {
            var prop = unplaced.Find(candidate => candidate.Model == item.Model);

            var decorModel = item.DecorModel;

            if ((prop is null && decorModel is null) || !IsFree(item.Bounds, obstacles))
            {
                continue;
            }

            obstacles.Add(item.Bounds);

            if (prop is null)
            {
                decor.Add(item with { Model = decorModel!.Value });
                continue;
            }

            unplaced.Remove(prop);
            bound.Add(new PlacedProp(prop.Id, prop.Model, item.Placement, item.Footprint));
        }

        decor.AddRange(items.Where(item => item.IsWallMounted && IsFree(item.Bounds, obstacles)));

        return unplaced.Count == 0
            ? new FurnishedRoom(bound, decor)
            : throw new InvalidOperationException(
                $"The recipe has no slot for {unplaced[0].Model} in a {room.Width}x{room.Depth} room."
            );
    }

    private static bool IsFree(RoomRect rect, IEnumerable<RoomRect> others) =>
        !others.Any(other => other.Width > 0 && other.Depth > 0 && rect.IsWithin(other, Clearance));
}
