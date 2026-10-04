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
    private static readonly IReadOnlyDictionary<PropModel, PropModel> StandIns = new Dictionary<
        PropModel,
        PropModel
    >
    {
        [PropModel.WorkstationReading] = PropModel.FurnitureBookcase,
        [PropModel.ContainerWeaponRack] = PropModel.FurnitureStaffRack,
    };

    private static readonly IReadOnlySet<PropModel> SittableDecor = new HashSet<PropModel>
    {
        PropModel.SeatChair,
        PropModel.SeatPew,
        PropModel.SeatBench,
    };

    internal bool IsSeat => SittableDecor.Contains(Model);

    internal PropModel? DecorModel =>
        SittableDecor.Contains(Model) ? Model
        : StandIns.TryGetValue(Model, out var standIn) ? standIn
        : Model.ToString().StartsWith(nameof(Furniture), StringComparison.Ordinal) ? Model
        : null;

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

internal record CounterAt(double Fraction, RecipeWall Wall = RecipeWall.North) : RecipeStep
{
    internal const double Setback = 1.1;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var size = RecipeGeometry.Rotated(
            PropFootprintCatalog.Get(PropModel.WorkstationTrade).Footprint,
            Wall
        );
        var alongX = Wall is RecipeWall.North or RecipeWall.South;
        var along = alongX ? AlongWall(room.Width, size.Width) : AlongWall(room.Depth, size.Depth);
        var offset = Setback + (alongX ? size.Depth : size.Width) / 2;

        return
        [
            Wall switch
            {
                RecipeWall.North => At(along, offset),
                RecipeWall.East => At(room.Width - offset, along),
                RecipeWall.South => At(along, room.Depth - offset),
                _ => At(offset, along),
            },
        ];
    }

    private RecipeItem At(double centerX, double centerY) =>
        RecipeGeometry.Around(PropModel.WorkstationTrade, centerX, centerY, Wall);

    private double AlongWall(double length, double extent) =>
        RecipeGeometry.Margin
        + Fraction * (length - extent - 2 * RecipeGeometry.Margin)
        + extent / 2;
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

internal record TableSetAt(double FractionX, double FractionY) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        RecipeGeometry.TableSet(FractionX * room.Width, FractionY * room.Depth);
}

internal record CenteredAt(
    PropModel Model,
    double FractionX,
    double FractionY,
    RecipeWall Wall = RecipeWall.North
) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [RecipeGeometry.Around(Model, FractionX * room.Width, FractionY * room.Depth, Wall)];
}

internal record CenteredFromSouth(PropModel Model, double Distance, RecipeWall Wall) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [RecipeGeometry.Around(Model, room.Width / 2, room.Depth - Distance, Wall)];
}

internal record RugRunner(double Width, double Top, double BottomInset) : RecipeStep
{
    internal override IEnumerable<RecipeItem> Expand(Footprint room) =>
        [
            RecipeGeometry.Rug(
                room.Width / 2,
                (Top + room.Depth - BottomInset) / 2,
                Width,
                room.Depth - BottomInset - Top
            ),
        ];
}

internal record PewRows : RecipeStep
{
    private const double RowSpacing = 1.8;
    private const double FirstRow = 4.5;
    private const double ChancelDepth = 12;
    private const double AisleMargin = 1.5;

    private static readonly double[] Offsets = [2.2, 4.4, 6.6];

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var rows = Math.Max(0, (int)Math.Floor((room.Depth - ChancelDepth) / RowSpacing));

        return Offsets
            .Where(offset => offset < room.Width / 2 - AisleMargin)
            .SelectMany(offset =>
                Enumerable
                    .Range(0, rows)
                    .SelectMany(row =>
                        new[] { -1, 1 }.Select(side =>
                            RecipeGeometry.Around(
                                PropModel.SeatPew,
                                room.Width / 2 + side * offset,
                                FirstRow + row * RowSpacing,
                                RecipeWall.North
                            )
                        )
                    )
            );
    }
}

internal record ReadingTables(double[] Columns) : RecipeStep
{
    private const double Spacing = 3.6;
    private const double FirstRow = 4.5;
    private const double SouthReserve = 5.2;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var rows = Math.Max(
            1,
            (int)Math.Floor((room.Depth - SouthReserve - FirstRow) / Spacing) + 1
        );

        return Columns.SelectMany(fraction =>
            Enumerable
                .Range(0, rows)
                .SelectMany(row =>
                    RecipeGeometry.TableSet(
                        room.Width * fraction,
                        rows == 1 ? room.Depth * 0.4 : FirstRow + row * Spacing
                    )
                )
        );
    }
}

internal record BookStacks(double[] Columns) : RecipeStep
{
    private const double RowSpacing = 1.15;
    private const double PairOffset = 0.425;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var count = Math.Max(1, (int)Math.Floor(room.Depth * 0.5 / RowSpacing));

        return Columns.SelectMany(fraction =>
            Enumerable
                .Range(0, count)
                .SelectMany(index =>
                {
                    var centerX = room.Width * fraction;
                    var centerY = room.Depth * 0.25 + room.Depth * 0.5 * (index + 0.5) / count;

                    return new[]
                    {
                        RecipeGeometry.Around(
                            PropModel.WorkstationReading,
                            centerX - PairOffset,
                            centerY,
                            RecipeWall.East
                        ),
                        RecipeGeometry.Around(
                            PropModel.WorkstationReading,
                            centerX + PairOffset,
                            centerY,
                            RecipeWall.West
                        ),
                    };
                })
        );
    }
}

internal record SouthStock(PropModel? Left, PropModel Right) : RecipeStep
{
    private const double LeftInset = 2;
    private const int RightSlots = 3;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var right = PropFootprintCatalog.Get(Right).Footprint;
        var rights = Enumerable
            .Range(0, RightSlots)
            .Select(slot =>
                AlongSouth(
                    room,
                    Right,
                    room.Width
                        - RecipeGeometry.Margin
                        - right.Width / 2
                        - slot * (right.Width + RecipeGeometry.RunGap)
                )
            );

        return Left is { } left
            ? rights.Prepend(
                AlongSouth(
                    room,
                    left,
                    LeftInset + PropFootprintCatalog.Get(left).Footprint.Width / 2
                )
            )
            : rights;
    }

    private static RecipeItem AlongSouth(Footprint room, PropModel model, double centerX) =>
        RecipeGeometry.Around(
            model,
            centerX,
            room.Depth
                - RecipeGeometry.Margin
                - PropFootprintCatalog.Get(model).Footprint.Depth / 2,
            RecipeWall.South
        );
}
