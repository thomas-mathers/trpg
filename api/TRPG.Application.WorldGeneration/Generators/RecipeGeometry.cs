using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RecipeGeometry
{
    internal const double Margin = 0.2;
    internal const double RunGap = 0.15;
    private const double ChairGap = 0.15;

    internal static double Facing(RecipeWall wall) =>
        wall switch
        {
            RecipeWall.North => Math.PI,
            RecipeWall.East => 3 * Math.PI / 2,
            RecipeWall.South => 0,
            _ => Math.PI / 2,
        };

    internal static Footprint Rotated(Footprint size, RecipeWall wall) =>
        wall is RecipeWall.East or RecipeWall.West ? new Footprint(size.Depth, size.Width) : size;

    internal static RecipeItem InsideRoom(
        Footprint room,
        PropModel model,
        double fractionX,
        double fractionY,
        RecipeWall wall
    )
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);
        var bounds = new RoomRect(
            Margin + fractionX * (room.Width - size.Width - 2 * Margin),
            Margin + fractionY * (room.Depth - size.Depth - 2 * Margin),
            size.Width,
            size.Depth
        );

        return new RecipeItem(model, bounds, wall);
    }

    internal static RecipeItem Around(
        PropModel model,
        double centerX,
        double centerY,
        RecipeWall wall
    )
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);
        var bounds = new RoomRect(
            centerX - size.Width / 2,
            centerY - size.Depth / 2,
            size.Width,
            size.Depth
        );

        return new RecipeItem(model, bounds, wall);
    }

    internal static RecipeItem Rug(double centerX, double centerY, double width, double depth) =>
        new(
            PropModel.FurnitureRug,
            new RoomRect(centerX - width / 2, centerY - depth / 2, width, depth),
            RecipeWall.North
        );

    internal static IEnumerable<RecipeItem> TableSet(double centerX, double centerY)
    {
        var table = PropFootprintCatalog.Get(PropModel.FurnitureTable);
        var chair = PropFootprintCatalog.Get(PropModel.SeatChair);
        var offset = table.Depth / 2 + ChairGap + chair.Depth / 2;

        yield return Around(PropModel.FurnitureTable, centerX, centerY, RecipeWall.North);
        yield return Around(PropModel.SeatChair, centerX, centerY - offset, RecipeWall.North);
        yield return Around(PropModel.SeatChair, centerX, centerY + offset, RecipeWall.South);
    }
}
