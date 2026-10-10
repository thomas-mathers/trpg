using TRPG.Domain.Models;

namespace TRPG.Tests.Domain;

public class PropBlocksMovementTests
{
    [Theory]
    [InlineData(PropModel.FurnitureTable)]
    [InlineData(PropModel.FurnitureTree)]
    [InlineData(PropModel.FurnitureStatue)]
    public void BlocksMovement_ReturnsTrue_WhenTheFurnitureStandsOnTheFloor(PropModel model)
    {
        // Arrange
        var prop = new Furniture { Model = model };

        // Act
        var blocks = prop.BlocksMovement;

        // Assert
        Assert.True(blocks);
    }

    [Theory]
    [InlineData(PropModel.FurnitureRug)]
    [InlineData(PropModel.FurnitureChandelier)]
    [InlineData(PropModel.FurnitureWallSconce)]
    [InlineData(PropModel.FurnitureWallLantern)]
    [InlineData(PropModel.FurnitureBanner)]
    public void BlocksMovement_ReturnsFalse_WhenTheFurnitureIsFlatOrMountedOutOfReach(
        PropModel model
    )
    {
        // Arrange
        var prop = new Furniture { Model = model };

        // Act
        var blocks = prop.BlocksMovement;

        // Assert
        Assert.False(blocks);
    }

    [Fact]
    public void BlocksMovement_ReturnsFalse_WhenThePropIsASign()
    {
        // Arrange
        var prop = new Sign();

        // Act
        var blocks = prop.BlocksMovement;

        // Assert
        Assert.False(blocks);
    }

    [Fact]
    public void BlocksMovement_ReturnsFalse_WhenThePropIsATrap()
    {
        // Arrange
        var prop = new Trap();

        // Act
        var blocks = prop.BlocksMovement;

        // Assert
        Assert.False(blocks);
    }

    [Fact]
    public void BlocksMovement_ReturnsFalse_WhenThePropIsATrigger()
    {
        // Arrange
        var prop = new Trigger();

        // Act
        var blocks = prop.BlocksMovement;

        // Assert
        Assert.False(blocks);
    }
}
