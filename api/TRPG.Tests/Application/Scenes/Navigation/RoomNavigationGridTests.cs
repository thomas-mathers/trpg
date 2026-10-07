using TRPG.Application.Scenes.Navigation;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes.Navigation;

public class RoomNavigationGridTests
{
    private static readonly Location Room = Builders.MakeLocation(width: 10, depth: 10);

    [Fact]
    public void FindPath_WalksAroundAFurnitureProp_WhenItSitsOnTheStraightLine()
    {
        // Arrange
        var grid = RoomNavigationGrid.Build(Room, [MakeFurniture(PropModel.FurnitureTable, 5, 5)]);

        // Act
        var path = grid.FindPath(new Point(1.25, 5), new Point(8.75, 5));

        // Assert
        Assert.True(path.Count > 2);
    }

    [Theory]
    [InlineData(PropModel.FurnitureRug)]
    [InlineData(PropModel.FurnitureWallSconce)]
    [InlineData(PropModel.FurnitureChandelier)]
    public void FindPath_WalksStraightThrough_WhenThePropDoesNotBlockTheFloor(PropModel model)
    {
        // Arrange
        var grid = RoomNavigationGrid.Build(Room, [MakeFurniture(model, 5, 5)]);

        // Act
        var path = grid.FindPath(new Point(1.25, 5), new Point(8.75, 5));

        // Assert
        Assert.Equal(2, path.Count);
    }

    [Fact]
    public void FindPath_TreatsAQuarterTurnedPropAsRotated()
    {
        // Arrange
        var prop = MakeFurniture(PropModel.FurnitureTable, 5, 5);
        prop.Width = 4;
        prop.Depth = 1;
        prop.Angle = Math.PI / 2;
        var grid = RoomNavigationGrid.Build(Room, [prop]);

        // Act
        var path = grid.FindPath(new Point(2, 5), new Point(8, 5));

        // Assert
        Assert.True(path.Count > 2);
    }

    private static Furniture MakeFurniture(PropModel model, double x, double y) =>
        new()
        {
            Model = model,
            X = x,
            Y = y,
            Width = 2,
            Depth = 2,
        };
}
