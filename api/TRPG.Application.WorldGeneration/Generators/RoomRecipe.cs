using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal enum RecipeWall
{
    North,
    East,
    South,
    West,
}

internal record RecipeItem(PropModel Model, RoomRect Bounds, RecipeWall Wall)
{
    internal Placement Placement =>
        new(Bounds.CenterX, Bounds.CenterY, RecipeGeometry.Facing(Wall));

    internal Footprint Footprint =>
        Wall is RecipeWall.East or RecipeWall.West
            ? new Footprint(Bounds.Depth, Bounds.Width)
            : new Footprint(Bounds.Width, Bounds.Depth);
}

internal record RoomRecipe(IReadOnlyList<RecipeStep> Steps)
{
    internal IReadOnlyList<RecipeItem> Expand(Footprint room) =>
        Steps.SelectMany(step => step.Expand(room)).ToArray();
}

internal abstract record RecipeStep
{
    internal abstract IEnumerable<RecipeItem> Expand(Footprint room);
}

internal record Anchored(
    PropModel Model,
    double FractionX,
    double FractionY,
    RecipeWall Wall = RecipeWall.North
) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [RecipeGeometry.InsideRoom(room, Model, FractionX, FractionY, Wall)];
}

internal record WallRun(
    PropModel Model,
    RecipeWall Wall,
    double From = 0.05,
    double To = 0.95,
    int? Count = null
) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var size = RecipeGeometry.Rotated(PropFootprintCatalog.Get(Model).Footprint, Wall);
        var horizontal = Wall is RecipeWall.North or RecipeWall.South;
        var length = horizontal ? room.Width : room.Depth;
        var along = horizontal ? size.Width : size.Depth;
        var count =
            Count
            ?? Math.Max(1, (int)Math.Floor((To - From) * length / (along + RecipeGeometry.RunGap)));

        return Enumerable
            .Range(0, count)
            .Select(index =>
                count == 1 ? (From + To) / 2 : From + (To - From) * index / (count - 1)
            )
            .Select(position => AtPosition(room, position));
    }

    private RecipeItem AtPosition(Footprint room, double position) =>
        Wall switch
        {
            RecipeWall.North => RecipeGeometry.InsideRoom(room, Model, position, 0, Wall),
            RecipeWall.South => RecipeGeometry.InsideRoom(room, Model, position, 1, Wall),
            RecipeWall.West => RecipeGeometry.InsideRoom(room, Model, 0, position, Wall),
            _ => RecipeGeometry.InsideRoom(room, Model, 1, position, Wall),
        };
}

internal record TableGrid(double Top, double Bottom) : RecipeStep
{
    private const double CellWidth = 5.5;
    private const double RowHeight = 4;
    private const double SideInset = 1.5;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var columns = Math.Max(1, (int)Math.Floor((room.Width - 2 * SideInset) / CellWidth));
        var rows = Math.Max(1, (int)Math.Floor((room.Depth - Top - Bottom) / RowHeight));

        return Enumerable
            .Range(0, columns * rows)
            .SelectMany(index =>
                RecipeGeometry.TableSet(
                    SideInset + (index % columns + 0.5) * (room.Width - 2 * SideInset) / columns,
                    Top + (index / columns + 0.5) * (room.Depth - Top - Bottom) / rows
                )
            );
    }
}

internal record RugAt(double FractionX, double FractionY, double Width, double Depth) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [RecipeGeometry.Rug(FractionX * room.Width, FractionY * room.Depth, Width, Depth)];
}

internal record RugBeside(RecipeWall Wall, double Width, double Depth) : RecipeStep
{
    private const double WallDistance = 1.9;

    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [
            RecipeGeometry.Rug(
                Wall == RecipeWall.East ? room.Width - WallDistance : WallDistance,
                room.Depth / 2,
                Width,
                Depth
            ),
        ];
}
