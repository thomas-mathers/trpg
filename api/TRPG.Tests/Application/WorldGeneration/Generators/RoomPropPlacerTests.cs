using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomPropPlacerTests
{
    private const double DoorKeepOutSize = 1.5;

    private static readonly Footprint Room = new(Width: 12, Depth: 10);

    private static readonly string[] MixedAssetKeys =
    [
        "prop.bed.basic",
        "prop.container.chest",
        "prop.container.barrel",
        "prop.workstation.cooking",
        "prop.workstation.trade",
        "prop.seat.chair",
        "prop.seat.chair",
        "prop.trigger.lever",
        "prop.sign.basic",
    ];

    private static RoomPropInput[] Props(params string[] assetKeys) =>
        assetKeys.Select(key => new RoomPropInput(Guid.NewGuid(), key)).ToArray();

    private static ConnectorExitRequest[] Doors() =>
        [
            new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.Compass)
            {
                Direction = CompassDirection.North,
            },
            new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.SouthDoor),
        ];

    private static RoomPlacementResult PlaceMixed(int seed, ConnectorExitRequest[] doors) =>
        RoomPropPlacer.Place(Room, Props(MixedAssetKeys), doors, new Random(seed));

    private static OrientedBox BoxOf(PlacedProp prop) =>
        OrientedBox.From(prop.Placement, prop.Footprint);

    private static OrientedBox KeepOutOf(ConnectorExit exit) =>
        new(
            exit.Point.X + DoorKeepOutSize / 2 * Math.Sin(exit.FacingAngle),
            exit.Point.Y - DoorKeepOutSize / 2 * Math.Cos(exit.FacingAngle),
            DoorKeepOutSize,
            DoorKeepOutSize,
            0
        );

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_KeepsEveryPropInsideTheRoom(int seed)
    {
        // Act
        var result = PlaceMixed(seed, Doors());

        // Assert
        Assert.Equal(MixedAssetKeys.Length, result.Props.Count);
        Assert.All(
            result.Props,
            prop => Assert.True(BoxOf(prop).IsInside(result.Room.Width, result.Room.Depth))
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_PlacesNoTwoPropsOverlapping(int seed)
    {
        // Act
        var result = PlaceMixed(seed, Doors());

        // Assert
        var boxes = result.Props.Select(BoxOf).ToArray();
        var overlapping = boxes.SelectMany(
            (box, index) => boxes.Skip(index + 1).Where(box.Overlaps)
        );
        Assert.Empty(overlapping);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_KeepsDoorKeepOutsClear(int seed)
    {
        // Act
        var result = PlaceMixed(seed, Doors());

        // Assert
        var keepOuts = result.Exits.Select(KeepOutOf).ToArray();
        Assert.DoesNotContain(result.Props, prop => keepOuts.Any(BoxOf(prop).Overlaps));
    }

    [Fact]
    public void Place_FacesASeatTowardItsWorkstation()
    {
        // Arrange
        var props = Props("prop.workstation.cooking", "prop.seat.chair");

        // Act
        var result = RoomPropPlacer.Place(Room, props, [], new Random(7));

        // Assert
        var workstation = result.Props.Single(prop => prop.Id == props[0].Id);
        var seat = result.Props.Single(prop => prop.Id == props[1].Id);
        var toWorkstationX = workstation.Placement.X - seat.Placement.X;
        var toWorkstationY = workstation.Placement.Y - seat.Placement.Y;
        var distance = Math.Sqrt(toWorkstationX * toWorkstationX + toWorkstationY * toWorkstationY);
        var facingDot =
            (
                Math.Sin(seat.Placement.Angle) * toWorkstationX
                - Math.Cos(seat.Placement.Angle) * toWorkstationY
            ) / distance;
        Assert.Equal(1, facingDot, precision: 6);
    }

    [Fact]
    public void Place_PutsABedInACorner()
    {
        // Arrange
        var props = Props("prop.bed.basic");

        // Act
        var result = RoomPropPlacer.Place(Room, props, [], new Random(5));

        // Assert
        var roomCorners = new[]
        {
            new PlanarPoint(0, 0),
            new PlanarPoint(Room.Width, 0),
            new PlanarPoint(0, Room.Depth),
            new PlanarPoint(Room.Width, Room.Depth),
        };
        var bedCorners = BoxOf(result.Props.Single()).Corners();
        Assert.Contains(
            roomCorners,
            roomCorner =>
                bedCorners.Any(bedCorner =>
                    Math.Abs(bedCorner.X - roomCorner.X) < 0.1
                    && Math.Abs(bedCorner.Y - roomCorner.Y) < 0.1
                )
        );
    }

    [Fact]
    public void Place_ReturnsTheSamePlacement_ForTheSameSeed()
    {
        // Arrange
        var props = Props(MixedAssetKeys);
        var doors = Doors();

        // Act
        var first = RoomPropPlacer.Place(Room, props, doors, new Random(11));
        var second = RoomPropPlacer.Place(Room, props, doors, new Random(11));

        // Assert
        Assert.Equal(first.Room, second.Room);
        Assert.Equal(first.Props, second.Props);
    }

    [Fact]
    public void Place_GrowsTheRoomAndStillPlacesEveryProp_WhenThePropsCannotFit()
    {
        // Arrange
        var tiny = new Footprint(Width: 3, Depth: 3);
        var props = Props(
            Enumerable
                .Repeat("prop.workstation.armorsmithing", 3)
                .Concat(Enumerable.Repeat("prop.container.chest", 8))
                .ToArray()
        );

        // Act
        var result = RoomPropPlacer.Place(tiny, props, Doors(), new Random(3));

        // Assert
        Assert.Equal(props.Length, result.Props.Count);
        Assert.True(result.Room.Width > tiny.Width);
        Assert.True(result.Room.Depth > tiny.Depth);
    }

    [Fact]
    public void Place_ReturnsExitsResolvedAgainstTheFinalRoom()
    {
        // Arrange
        var tiny = new Footprint(Width: 3, Depth: 3);
        var props = Props(Enumerable.Repeat("prop.container.chest", 8).ToArray());
        var doors = Doors();

        // Act
        var result = RoomPropPlacer.Place(tiny, props, doors, new Random(3));

        // Assert
        var expected = ConnectorPointResolver.ResolveExits(result.Room, doors);
        Assert.Equal(expected, result.Exits);
    }
}
