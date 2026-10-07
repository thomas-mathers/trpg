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

    internal bool IsOverlay => Model is PropModel.FurnitureRug or PropModel.FurnitureChandelier;

    internal bool IsCeilingMounted => Model == PropModel.FurnitureChandelier;

    internal bool IsWallMounted => Model == PropModel.FurnitureWallSconce;

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
    internal const double Setback = 1.0;

    internal override IEnumerable<RecipeItem> Expand(Footprint room)
    {
        var size = RecipeGeometry.Rotated(
            PropFootprintCatalog.Get(PropModel.WorkstationTrade).Footprint,
            Wall
        );
        var alongX = Wall is RecipeWall.North or RecipeWall.South;
        var along = alongX
            ? RecipeGeometry.AlongRoom(room.Width, size.Width, Fraction)
            : RecipeGeometry.AlongRoom(room.Depth, size.Depth, Fraction);

        return [RecipeGeometry.OnWall(room, PropModel.WorkstationTrade, Wall, Setback, along)];
    }
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

        var setback = FlushModels.Contains(Model) ? 0 : RoomGrid.CellSize;

        return Lefts(length, along)
            .Select(left => RecipeGeometry.OnWall(room, Model, Wall, setback, left));
    }

    private static readonly IReadOnlySet<PropModel> FlushModels = new HashSet<PropModel>
    {
        PropModel.FurnitureWallSconce,
        PropModel.FurnitureFireplace,
    };

    private IEnumerable<double> Lefts(double length, double extent)
    {
        if (Count is { } count)
        {
            return Enumerable
                .Range(0, count)
                .Select(index =>
                    RecipeGeometry.AlongRoom(
                        length,
                        extent,
                        count == 1 ? (From + To) / 2 : From + (To - From) * index / (count - 1)
                    )
                );
        }

        var free = length - extent;
        var cell = RoomGrid.SnapUp(extent);
        var span = (To - From) * free;
        var fits = Math.Max(1, (int)Math.Floor(span / cell + 1e-9) + 1);

        if (fits == 1)
        {
            return [RecipeGeometry.AlongRoom(length, extent, (From + To) / 2)];
        }

        var step = RoomGrid.SnapDown(span / (fits - 1));
        var start = RoomGrid.SnapDown(From * free);

        return Enumerable.Range(0, fits).Select(index => start + index * step);
    }
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
    private const double PairOffset = 0.5;

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
        var cell = RoomGrid.SnapUp(right.Width);
        var rights = Enumerable
            .Range(0, RightSlots)
            .Select(slot =>
                AlongSouth(
                    room,
                    Right,
                    RecipeGeometry.FarEdge(room.Width, 0, right.Width) - slot * cell
                )
            );

        return Left is { } left ? rights.Prepend(AlongSouth(room, left, LeftInset)) : rights;
    }

    private static RecipeItem AlongSouth(Footprint room, PropModel model, double left) =>
        RecipeGeometry.OnWall(room, model, RecipeWall.South, 0, left);
}
