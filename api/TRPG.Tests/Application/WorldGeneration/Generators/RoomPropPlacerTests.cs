using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomPropPlacerTests
{
    private const double DoorKeepOutSize = 1.5;

    private static readonly Footprint Room = new(Width: 12, Depth: 10);

    private static readonly PropModel[] MixedModels =
    [
        PropModel.Bed,
        PropModel.ContainerChest,
        PropModel.ContainerBarrel,
        PropModel.WorkstationCooking,
        PropModel.WorkstationTrade,
        PropModel.SeatChair,
        PropModel.SeatChair,
        PropModel.TriggerLever,
        PropModel.Sign,
    ];

    private static RoomPropInput[] Props(params PropModel[] models) =>
        models.Select(model => new RoomPropInput(Guid.NewGuid(), model)).ToArray();

    private static ConnectorExitRequest[] Doors() =>
        [
            new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.Compass)
            {
                Direction = CompassDirection.North,
            },
            new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.SouthDoor),
        ];

    private static RoomPlacementResult PlaceMixed(int seed, ConnectorExitRequest[] doors) =>
        RoomPropPlacer.Place(Room, Props(MixedModels), doors, new Random(seed));

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
        Assert.Equal(MixedModels.Length, result.Props.Count);
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
        var props = Props(PropModel.WorkstationCooking, PropModel.SeatChair);

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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Place_LinesSeatsUpInFrontOfTheTradeWorkstation_BeforeSeatingOtherWorkstations(
        int seed
    )
    {
        // Arrange
        var props = Props(
            PropModel.WorkstationCooking,
            PropModel.WorkstationTrade,
            PropModel.SeatChair,
            PropModel.SeatChair,
            PropModel.SeatChair
        );

        // Act
        var result = RoomPropPlacer.Place(Room, props, [], new Random(seed));

        // Assert
        var trade = result.Props.Single(prop => prop.Model == PropModel.WorkstationTrade);
        var seats = result.Props.Where(prop => prop.Model == PropModel.SeatChair).ToArray();
        Assert.All(seats, seat => Assert.True(IsInFrontOf(trade, seat)));
    }

    [Fact]
    public void Place_CentersTheTradeWorkstation_WhenOnlyASouthDoorLimitsTheRoom()
    {
        // Arrange
        var tavernRoom = new Footprint(Width: 6, Depth: 5);
        var props = Props(PropModel.WorkstationTrade);
        ConnectorExitRequest[] southDoor =
        [
            new(Guid.NewGuid(), Guid.NewGuid(), ConnectorExitKind.SouthDoor),
        ];

        // Act
        var result = RoomPropPlacer.Place(tavernRoom, props, southDoor, new Random(1));

        // Assert
        var placement = result.Props.Single().Placement;
        Assert.Equal(tavernRoom.Width / 2, placement.X);
        Assert.Equal(tavernRoom.Depth / 2, placement.Y);
    }

    private static bool IsInFrontOf(PlacedProp workstation, PlacedProp seat)
    {
        var offsetX = seat.Placement.X - workstation.Placement.X;
        var offsetY = seat.Placement.Y - workstation.Placement.Y;
        var angle = workstation.Placement.Angle;
        var forward = offsetX * Math.Sin(angle) - offsetY * Math.Cos(angle);
        var lateral = offsetX * Math.Cos(angle) + offsetY * Math.Sin(angle);

        return forward > workstation.Footprint.Depth / 2
            && Math.Abs(lateral) <= workstation.Footprint.Width / 2 + 1e-6;
    }

    [Fact]
    public void Place_PutsABedInACorner()
    {
        // Arrange
        var props = Props(PropModel.Bed);

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
        var props = Props(MixedModels);
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
                .Repeat(PropModel.WorkstationArmorsmithing, 3)
                .Concat(Enumerable.Repeat(PropModel.ContainerChest, 8))
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
        var props = Props(Enumerable.Repeat(PropModel.ContainerChest, 8).ToArray());
        var doors = Doors();

        // Act
        var result = RoomPropPlacer.Place(tiny, props, doors, new Random(3));

        // Assert
        var expected = ConnectorPointResolver.ResolveExits(result.Room, doors);
        Assert.Equal(expected, result.Exits);
    }
}
