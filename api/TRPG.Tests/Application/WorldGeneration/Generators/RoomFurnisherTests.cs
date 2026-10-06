using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomFurnisherTests
{
    private static readonly Footprint Room = new(6, 6);
    private static readonly RoomRecipe BedroomRecipe = new([
        new RugAt(0.5, 0.5, 1.5, 1.5),
        new Anchored(PropModel.Bed, 0, 0),
        new Anchored(PropModel.ContainerChest, 1, 1),
        new Anchored(PropModel.FurnitureFireplace, 1, 0.5, RecipeWall.East),
    ]);

    [Fact]
    public void Furnish_BindsEachPropToASlotOfItsModel()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var chest = new RoomPropInput(Guid.NewGuid(), PropModel.ContainerChest);

        // Act
        var result = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed, chest], []);

        // Assert
        Assert.Equal([bed.Id, chest.Id], result.Bound.Select(prop => prop.Id).ToArray());
    }

    [Fact]
    public void Furnish_KeepsEverythingInsideTheRoomWithoutOverlap()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var chest = new RoomPropInput(Guid.NewGuid(), PropModel.ContainerChest);

        // Act
        var result = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed, chest], []);

        // Assert
        var boxes = result
            .Bound.Select(prop => OrientedBox.From(prop.Placement, prop.Footprint))
            .Concat(
                result
                    .Decor.Where(item => item.Model != PropModel.FurnitureRug)
                    .Select(item => OrientedBox.From(item.Placement, item.Footprint))
            )
            .ToArray();
        Assert.All(boxes, box => Assert.True(box.IsInside(Room.Width, Room.Depth)));
        Assert.Empty(boxes.SelectMany((box, index) => boxes.Skip(index + 1).Where(box.Overlaps)));
    }

    [Fact]
    public void Furnish_SpawnsDecorForFurnitureSlots()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);

        // Act
        var result = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed], []);

        // Assert
        Assert.Contains(result.Decor, item => item.Model == PropModel.FurnitureFireplace);
    }

    [Theory]
    [InlineData(PropModel.SeatChair)]
    [InlineData(PropModel.SeatPew)]
    [InlineData(PropModel.SeatBench)]
    public void Furnish_KeepsASeatSlotAsASeat_WhenNoPropFillsIt(PropModel slotModel)
    {
        // Arrange
        var recipe = new RoomRecipe([new Anchored(slotModel, 0.5, 0.5)]);

        // Act
        var result = RoomFurnisher.Furnish(Room, recipe, [], []);

        // Assert
        Assert.Equal(slotModel, Assert.Single(result.Decor).Model);
    }

    [Theory]
    [InlineData(PropModel.WorkstationReading, PropModel.FurnitureBookcase)]
    [InlineData(PropModel.ContainerWeaponRack, PropModel.FurnitureStaffRack)]
    public void Furnish_SpawnsAStandInDecor_WhenNoPropFillsAGameplaySlot(
        PropModel slotModel,
        PropModel standIn
    )
    {
        // Arrange
        var recipe = new RoomRecipe([new Anchored(slotModel, 0.5, 0.5)]);

        // Act
        var result = RoomFurnisher.Furnish(Room, recipe, [], []);

        // Assert
        Assert.Equal(standIn, Assert.Single(result.Decor).Model);
    }

    [Fact]
    public void Furnish_DropsAGameplaySlot_WhenNoPropFillsIt()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);

        // Act
        var result = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed], []);

        // Assert
        Assert.Single(result.Bound);
    }

    [Fact]
    public void Furnish_Throws_WhenAPropHasNoSlot()
    {
        // Arrange
        var beds = new[]
        {
            new RoomPropInput(Guid.NewGuid(), PropModel.Bed),
            new RoomPropInput(Guid.NewGuid(), PropModel.Bed),
        };

        // Act
        var act = () => RoomFurnisher.Furnish(Room, BedroomRecipe, beds, []);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Furnish_Throws_WhenAKeepOutBlocksTheOnlySlot()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var keepOut = new RoomRect(0, 0, 3, 3);

        // Act
        var act = () => RoomFurnisher.Furnish(Room, BedroomRecipe, [bed], [keepOut]);

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    [Fact]
    public void Furnish_DropsARug_WhenAKeepOutCoversIt()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var keepOut = new RoomRect(2.5, 2.5, 1, 1);

        // Act
        var result = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed], [keepOut]);

        // Assert
        Assert.DoesNotContain(result.Decor, item => item.Model == PropModel.FurnitureRug);
    }

    [Fact]
    public void Furnish_HangsAChandelierOverFurniture_WithoutClaimingTheFloor()
    {
        // Arrange
        RoomRecipe recipe = new([
            new Anchored(PropModel.Bed, 0.5, 0.5),
            new CenteredAt(PropModel.FurnitureChandelier, 0.5, 0.5),
        ]);
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);

        // Act
        var result = RoomFurnisher.Furnish(Room, recipe, [bed], []);

        // Assert
        Assert.Contains(result.Decor, item => item.Model == PropModel.FurnitureChandelier);
        Assert.Single(result.Bound);
    }

    [Fact]
    public void Furnish_DropsAWallSconce_WhenFurnitureAlreadyOccupiesTheSpot()
    {
        // Arrange
        RoomRecipe recipe = new([
            new Anchored(PropModel.Bed, 0.5, 0),
            new Anchored(PropModel.FurnitureWallSconce, 0.5, 0),
        ]);
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);

        // Act
        var result = RoomFurnisher.Furnish(Room, recipe, [bed], []);

        // Assert
        Assert.DoesNotContain(result.Decor, item => item.Model == PropModel.FurnitureWallSconce);
    }

    [Fact]
    public void Furnish_KeepsAWallSconce_WhenTheWallSpotIsFree()
    {
        // Arrange
        RoomRecipe recipe = new([new Anchored(PropModel.FurnitureWallSconce, 0.5, 0)]);

        // Act
        var result = RoomFurnisher.Furnish(Room, recipe, [], []);

        // Assert
        Assert.Contains(result.Decor, item => item.Model == PropModel.FurnitureWallSconce);
    }

    [Fact]
    public void Furnish_DropsFurnitureThatFallsOutsideTheRoom()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var smallRoom = new Footprint(1.5, 3);
        var recipe = new RoomRecipe([
            new Anchored(PropModel.Bed, 0, 0),
            new Anchored(PropModel.FurnitureStall, 1, 1),
        ]);

        // Act
        var result = RoomFurnisher.Furnish(smallRoom, recipe, [bed], []);

        // Assert
        Assert.Empty(result.Decor);
    }

    [Fact]
    public void Furnish_ProducesTheSameLayout_WhenRunTwice()
    {
        // Arrange
        var bed = new RoomPropInput(Guid.NewGuid(), PropModel.Bed);
        var chest = new RoomPropInput(Guid.NewGuid(), PropModel.ContainerChest);
        var first = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed, chest], []);

        // Act
        var second = RoomFurnisher.Furnish(Room, BedroomRecipe, [bed, chest], []);

        // Assert
        Assert.Equal(first.Bound, second.Bound);
        Assert.Equal(first.Decor, second.Decor);
    }

    [Theory]
    [InlineData(0, 5, 4.25)]
    [InlineData(Math.PI / 2, 5.75, 5)]
    public void KeepOut_CentersAHalfSquareAheadOfTheExit(
        double facing,
        double expectedX,
        double expectedY
    )
    {
        // Arrange
        var exit = new ConnectorExit(Guid.NewGuid(), new PlanarPoint(5, 5), facing);

        // Act
        var keepOut = RoomFurnisher.KeepOut(exit);

        // Assert
        Assert.Equal(expectedX, keepOut.CenterX, 1e-9);
        Assert.Equal(expectedY, keepOut.CenterY, 1e-9);
    }
}
