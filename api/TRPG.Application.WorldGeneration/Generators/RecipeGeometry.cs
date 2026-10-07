using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class RecipeGeometry
{
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

    internal static double AlongRoom(double length, double extent, double fraction)
    {
        var free = length - extent;

        return fraction switch
        {
            <= 0 => 0,
            >= 1 => RoomGrid.SnapDown(free),
            _ => Math.Min(RoomGrid.SnapNearest(fraction * free), RoomGrid.SnapDown(free)),
        };
    }

    internal static RecipeItem At(PropModel model, double left, double top, RecipeWall wall)
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);

        return new RecipeItem(model, new RoomRect(left, top, size.Width, size.Depth), wall);
    }

    internal static RecipeItem InsideRoom(
        Footprint room,
        PropModel model,
        double fractionX,
        double fractionY,
        RecipeWall wall
    )
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);

        return At(
            model,
            AlongRoom(room.Width, size.Width, fractionX),
            AlongRoom(room.Depth, size.Depth, fractionY),
            wall
        );
    }

    internal static RecipeItem OnWall(
        Footprint room,
        PropModel model,
        RecipeWall wall,
        double setback,
        double along
    )
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);

        return wall switch
        {
            RecipeWall.North => At(model, along, setback, wall),
            RecipeWall.East => At(model, FarEdge(room.Width, setback, size.Width), along, wall),
            RecipeWall.South => At(model, along, FarEdge(room.Depth, setback, size.Depth), wall),
            _ => At(model, setback, along, wall),
        };
    }

    internal static double FarEdge(double length, double setback, double extent) =>
        RoomGrid.SnapDown(length - setback - extent);

    internal static RecipeItem Around(
        PropModel model,
        double centerX,
        double centerY,
        RecipeWall wall
    )
    {
        var size = Rotated(PropFootprintCatalog.Get(model).Footprint, wall);

        return At(
            model,
            RoomGrid.SnapNearest(centerX - size.Width / 2),
            RoomGrid.SnapNearest(centerY - size.Depth / 2),
            wall
        );
    }

    internal static RecipeItem Rug(double centerX, double centerY, double width, double depth) =>
        new(
            PropModel.FurnitureRug,
            new RoomRect(centerX - width / 2, centerY - depth / 2, width, depth),
            RecipeWall.North
        );

    internal static IEnumerable<RecipeItem> TableSet(double centerX, double centerY)
    {
        var table = Around(PropModel.FurnitureTable, centerX, centerY, RecipeWall.North);
        var chair = PropFootprintCatalog.Get(PropModel.SeatChair).Footprint;
        var chairLeft = RoomGrid.SnapNearest(table.Bounds.CenterX - chair.Width / 2);
        var tableBottom = table.Bounds.Top + table.Bounds.Depth;

        yield return table;
        yield return At(
            PropModel.SeatChair,
            chairLeft,
            table.Bounds.Top - RoomGrid.SnapUp(chair.Depth),
            RecipeWall.North
        );
        yield return At(
            PropModel.SeatChair,
            chairLeft,
            RoomGrid.SnapUp(tableBottom),
            RecipeWall.South
        );
    }
}
