using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class LocationSizerTests
{
    private static readonly PropModel[] BedModels = [PropModel.Bed];

    private static RoomSizingRequest Request(
        BuildingType buildingType = BuildingType.House,
        RoomRole? role = null,
        IReadOnlyCollection<PropModel>? propModels = null,
        int capacity = 1
    ) => new(buildingType, role, propModels ?? BedModels, capacity);

    private static bool IsOnGrid(double value) =>
        Math.Abs(value / LocationSizer.GridSize - Math.Round(value / LocationSizer.GridSize))
        < 1e-9;

    [Fact]
    public void SizeRoom_ReturnsAtLeastTheCatalogMinimumArea_WhenThereAreNoProps()
    {
        // Arrange
        var request = Request(propModels: [], capacity: 0);

        // Act
        var footprint = LocationSizer.SizeRoom(request, new Random(1));

        // Assert
        Assert.True(
            footprint.Width * footprint.Depth
                >= RoomSizeCatalog.Get(BuildingType.House, null).MinimumArea
        );
    }

    [Fact]
    public void SizeRoom_NeverExceedsTheCatalogMaximumArea_WhenPropsAreNumerous()
    {
        // Arrange
        var request = Request(
            propModels: Enumerable.Repeat(PropModel.Bed, 40).ToArray(),
            capacity: 30
        );

        // Act
        var footprint = LocationSizer.SizeRoom(request, new Random(1));

        // Assert
        Assert.True(
            footprint.Width * footprint.Depth
                <= RoomSizeCatalog.Get(BuildingType.House, null).MaximumArea
        );
    }

    [Fact]
    public void SizeRoom_GrowsWithPropFootprints_WhenAboveTheMinimum()
    {
        // Arrange
        var small = Request(buildingType: BuildingType.Inn, propModels: [PropModel.Bed]);
        var large = Request(
            buildingType: BuildingType.Inn,
            propModels: Enumerable.Repeat(PropModel.Bed, 10).ToArray()
        );

        // Act
        var smallFootprint = LocationSizer.SizeRoom(small, new Random(1));
        var largeFootprint = LocationSizer.SizeRoom(large, new Random(1));

        // Assert
        Assert.True(
            largeFootprint.Width * largeFootprint.Depth
                > smallFootprint.Width * smallFootprint.Depth
        );
    }

    [Fact]
    public void SizeRoom_SnapsBothDimensionsToTheGrid()
    {
        // Act
        var footprint = LocationSizer.SizeRoom(Request(), new Random(7));

        // Assert
        Assert.True(IsOnGrid(footprint.Width));
        Assert.True(IsOnGrid(footprint.Depth));
    }

    [Fact]
    public void SizeRoom_ReturnsTheSameFootprint_ForTheSameSeed()
    {
        // Arrange
        var request = Request(buildingType: BuildingType.Tavern);

        // Act
        var first = LocationSizer.SizeRoom(request, new Random(42));
        var second = LocationSizer.SizeRoom(request, new Random(42));

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void SizeRoom_UsesTheRoleLimits_WhenARoleIsGiven()
    {
        // Arrange
        var request = Request(
            buildingType: BuildingType.Crypt,
            role: RoomRole.BossChamber,
            propModels: []
        );

        // Act
        var footprint = LocationSizer.SizeRoom(request, new Random(1));

        // Assert
        Assert.True(
            footprint.Width * footprint.Depth
                >= RoomSizeCatalog.Get(BuildingType.Crypt, RoomRole.BossChamber).MinimumArea
        );
    }

    [Theory]
    [InlineData(1, 3.5)]
    [InlineData(2, 5)]
    [InlineData(6, 11)]
    public void SizeHallway_IsTwoMetersWideAndScalesDepthWithRoomCount(
        int roomCount,
        double expectedDepth
    )
    {
        // Act
        var footprint = LocationSizer.SizeHallway(roomCount);

        // Assert
        Assert.Equal(new Footprint(Width: 2, Depth: expectedDepth), footprint);
    }

    [Fact]
    public void SizeBuilding_AddsCirculationToTheGroundFloorArea()
    {
        // Act
        var footprint = LocationSizer.SizeBuilding(BuildingType.House, 100, new Random(1));

        // Assert
        Assert.True(footprint.Width * footprint.Depth >= 115);
    }

    [Fact]
    public void SizeBuilding_UsesTheCatalogMinimum_ForSmallGroundFloors()
    {
        // Act
        var footprint = LocationSizer.SizeBuilding(BuildingType.Castle, 20, new Random(1));

        // Assert
        Assert.True(
            footprint.Width * footprint.Depth
                >= BuildingFootprintCatalog.GetMinimumArea(BuildingType.Castle)
        );
    }

    [Fact]
    public void SizeBuilding_ReturnsTheSameFootprint_ForTheSameSeed()
    {
        // Act
        var first = LocationSizer.SizeBuilding(BuildingType.Inn, 120, new Random(5));
        var second = LocationSizer.SizeBuilding(BuildingType.Inn, 120, new Random(5));

        // Assert
        Assert.Equal(first, second);
    }

    [Fact]
    public void SizeDistrict_CoversTheBuildingsWithMarginAndSlack()
    {
        // Arrange
        var buildings = Enumerable.Repeat(new Footprint(Width: 10, Depth: 8), 10).ToArray();

        // Act
        var footprint = LocationSizer.SizeDistrict(buildings, 0);

        // Assert
        Assert.True(footprint.Width * footprint.Depth >= 2 * 10 * (16 * 14));
    }

    [Fact]
    public void SizeDistrict_FitsTheWidestAndDeepestBuilding_WhenOneIsLarge()
    {
        // Arrange
        var buildings = new[] { new Footprint(Width: 40, Depth: 30) };

        // Act
        var footprint = LocationSizer.SizeDistrict(buildings, 0);

        // Assert
        Assert.True(footprint.Width >= 46);
        Assert.True(footprint.Depth >= 6 + 2 * 33);
    }

    [Fact]
    public void SizeDistrict_ReturnsAMinimumSize_WhenThereAreNoBuildings()
    {
        // Act
        var footprint = LocationSizer.SizeDistrict([], 0);

        // Assert
        Assert.Equal(new Footprint(Width: 20, Depth: 20), footprint);
    }

    [Fact]
    public void SizeWilderness_Returns300By300_RegardlessOfInput()
    {
        // Act
        var footprint = LocationSizer.SizeWilderness();

        // Assert
        Assert.Equal(new Footprint(Width: 300, Depth: 300), footprint);
    }
}
