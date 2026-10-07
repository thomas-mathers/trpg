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
        var cells = new List<RoomRect>();

        foreach (var recipeItem in items.Where(item => !item.IsOverlay && !item.IsWallMounted))
        {
            var prop = unplaced.Find(candidate => candidate.Model == recipeItem.Model);
            var decorModel = recipeItem.DecorModel;

            if (prop is null && decorModel is null)
            {
                continue;
            }

            var item = AlignToFreeCells(recipeItem, room, blocked, cells);

            if (item is null)
            {
                continue;
            }

            cells.Add(CellRect(item.Placement, item.Footprint));

            if (prop is null)
            {
                decor.Add(item with { Model = decorModel!.Value });
                continue;
            }

            unplaced.Remove(prop);
            bound.Add(new PlacedProp(prop.Id, prop.Model, item.Placement, item.Footprint));
        }

        var obstacles = blocked.Concat(cells).ToArray();
        decor.AddRange(items.Where(item => item.IsWallMounted && IsFree(item.Bounds, obstacles)));

        return unplaced.Count == 0
            ? new FurnishedRoom(bound, decor)
            : throw new InvalidOperationException(
                $"The recipe has no slot for {unplaced[0].Model} in a {room.Width}x{room.Depth} room."
            );
    }

    private static RecipeItem? AlignToFreeCells(
        RecipeItem item,
        Footprint room,
        IReadOnlyCollection<RoomRect> blocked,
        IReadOnlyCollection<RoomRect> cells
    )
    {
        foreach (var pose in RoomGrid.Alignments(item.Placement, item.Footprint))
        {
            var cell = CellRect(pose, item.Footprint);

            if (
                cell.IsInside(room)
                && IsFree(cell, blocked)
                && cells.All(other => !cell.IsWithin(other, margin: 0))
            )
            {
                return Shifted(item, pose.X - item.Placement.X, pose.Y - item.Placement.Y);
            }
        }

        return null;
    }

    private static RecipeItem Shifted(RecipeItem item, double shiftX, double shiftY)
    {
        if (shiftX == 0 && shiftY == 0)
        {
            return item;
        }

        var bounds = item.Bounds;

        return item with
        {
            Bounds = new RoomRect(
                bounds.Left + shiftX,
                bounds.Top + shiftY,
                bounds.Width,
                bounds.Depth
            ),
        };
    }

    private static RoomRect CellRect(Placement pose, Footprint footprint)
    {
        var box = RoomGrid.CellBox(pose, footprint);

        return new RoomRect(
            box.CenterX - box.Width / 2,
            box.CenterY - box.Depth / 2,
            box.Width,
            box.Depth
        );
    }

    private static bool IsFree(RoomRect rect, IEnumerable<RoomRect> others) =>
        !others.Any(other => other.Width > 0 && other.Depth > 0 && rect.IsWithin(other, Clearance));
}
