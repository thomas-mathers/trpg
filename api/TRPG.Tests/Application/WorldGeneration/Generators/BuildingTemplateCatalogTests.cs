using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BuildingTemplateCatalogTests
{
    private static readonly BuildingType[] CityTypes = Enum.GetValues<BuildingType>()
        .Except(BuildingTypes.Dungeon)
        .ToArray();

    public static TheoryData<BuildingType> CityBuildingTypes => new(CityTypes);

    public static TheoryData<BuildingType, int> TemplateIndexes => MakeTemplateIndexes();

    private static TheoryData<BuildingType, int> MakeTemplateIndexes()
    {
        var data = new TheoryData<BuildingType, int>();

        foreach (var type in CityTypes)
        {
            for (var index = 0; index < BuildingTemplateCatalog.GetTemplates(type).Count; index++)
            {
                data.Add(type, index);
            }
        }

        return data;
    }

    private static BuildingTemplate TemplateAt(BuildingType type, int index) =>
        BuildingTemplateCatalog.GetTemplates(type)[index];

    private static bool IsOnGrid(double value) =>
        Math.Abs(value / LocationSizer.GridSize - Math.Round(value / LocationSizer.GridSize))
        < 1e-9;

    private static Room[] RoomsOf(BuildingTemplate template) =>
        template
            .Floors.SelectMany(
                (floor, floorNumber) =>
                    floor.Rooms.Select(room =>
                        Builders.MakeRoom(Guid.NewGuid(), name: room.Name, floorNumber: floorNumber)
                    )
            )
            .ToArray();

    [Fact]
    public void BuildingTypes_CoverEveryNonDungeonBuildingType()
    {
        // Act
        var types = BuildingTemplateCatalog.BuildingTypes;

        // Assert
        Assert.Equal(CityTypes.Order(), types.Order());
    }

    [Theory]
    [MemberData(nameof(TemplateIndexes))]
    public void Footprint_IsSnappedToTheGrid(BuildingType type, int index)
    {
        // Arrange
        var template = TemplateAt(type, index);

        // Assert
        Assert.True(IsOnGrid(template.Footprint.Width));
        Assert.True(IsOnGrid(template.Footprint.Depth));
    }

    [Theory]
    [MemberData(nameof(TemplateIndexes))]
    public void RoomSizes_AreSnappedToTheGrid(BuildingType type, int index)
    {
        // Arrange
        var template = TemplateAt(type, index);

        // Act
        var sizes = RoomsOf(template)
            .Select(room => template.RoomSize(room))
            .Concat(template.Floors.Select(floor => floor.Hallway).OfType<Footprint>())
            .ToArray();

        // Assert
        Assert.All(
            sizes,
            size =>
            {
                Assert.True(IsOnGrid(size.Width));
                Assert.True(IsOnGrid(size.Depth));
            }
        );
    }

    [Theory]
    [MemberData(nameof(TemplateIndexes))]
    public void Floors_HaveAHallwayExactlyWhenTheyHoldSeveralRooms(BuildingType type, int index)
    {
        // Arrange
        var template = TemplateAt(type, index);

        // Assert
        Assert.All(
            template.Floors,
            floor => Assert.Equal(floor.Rooms.Count > 1, floor.Hallway is not null)
        );
    }

    [Theory]
    [MemberData(nameof(TemplateIndexes))]
    public void Rooms_FitInsideTheBuildingFootprint(BuildingType type, int index)
    {
        // Arrange
        var template = TemplateAt(type, index);

        // Assert
        Assert.All(template.Floors, floor => AssertFloorFits(template.Footprint, floor));
    }

    [Theory]
    [MemberData(nameof(TemplateIndexes))]
    public void Hallway_SpansTheBuildingDepth(BuildingType type, int index)
    {
        // Arrange
        var template = TemplateAt(type, index);

        // Assert
        Assert.All(
            template.Floors.Where(floor => floor.Hallway is not null),
            floor => Assert.Equal(template.Footprint.Depth, floor.Hallway!.Depth)
        );
    }

    [Theory]
    [MemberData(nameof(CityBuildingTypes))]
    public void Resolve_ReturnsATemplateCoveringTheFullRoomList_ForEveryBuildingType(
        BuildingType type
    )
    {
        // Arrange
        var rooms = RoomsOf(BuildingTemplateCatalog.GetTemplates(type)[^1]);

        // Act
        var template = BuildingTemplateCatalog.Resolve(type, rooms);

        // Assert
        Assert.True(template.Covers(rooms));
    }

    [Theory]
    [InlineData(1, "Small")]
    [InlineData(2, "Medium")]
    [InlineData(3, "Large")]
    public void Resolve_ReturnsTheSmallestHouse_ThatHasEnoughBedrooms(
        int bedroomCount,
        string expectedName
    )
    {
        // Arrange
        var rooms = Enumerable
            .Range(1, bedroomCount)
            .Select(number =>
                Builders.MakeRoom(Guid.NewGuid(), name: $"Bedroom {number}", floorNumber: 1)
            )
            .Append(Builders.MakeRoom(Guid.NewGuid(), name: "Living Room"))
            .ToArray();

        // Act
        var template = BuildingTemplateCatalog.Resolve(BuildingType.House, rooms);

        // Assert
        Assert.Equal(expectedName, template.Name);
    }

    [Fact]
    public void Resolve_Throws_WhenNoTemplateHasARoomForEveryName()
    {
        // Arrange
        var rooms = Enumerable
            .Range(1, 4)
            .Select(number =>
                Builders.MakeRoom(Guid.NewGuid(), name: $"Bedroom {number}", floorNumber: 1)
            )
            .ToArray();

        // Act
        var resolve = () => BuildingTemplateCatalog.Resolve(BuildingType.House, rooms);

        // Assert
        Assert.Throws<InvalidOperationException>(resolve);
    }

    [Fact]
    public void Resolve_IgnoresTheSynthesizedHallway()
    {
        // Arrange
        var rooms = new[]
        {
            Builders.MakeRoom(Guid.NewGuid(), name: "Living Room"),
            Builders.MakeRoom(Guid.NewGuid(), name: "Bedroom 1", floorNumber: 1),
            Builders.MakeRoom(Guid.NewGuid(), name: BuildingGenerator.HallwayName, floorNumber: 1),
        };

        // Act
        var template = BuildingTemplateCatalog.Resolve(BuildingType.House, rooms);

        // Assert
        Assert.Equal("Small", template.Name);
    }

    [Fact]
    public void RoomSize_ReturnsTheBuildingFootprint_ForAnOpenRoom()
    {
        // Arrange
        var template = BuildingTemplateCatalog.GetTemplates(BuildingType.Blacksmith).Single();
        var workshop = Builders.MakeRoom(Guid.NewGuid(), name: "Workshop");

        // Act
        var size = template.RoomSize(workshop);

        // Assert
        Assert.Equal(template.Footprint, size);
    }

    private static void AssertFloorFits(Footprint building, TemplateFloor floor)
    {
        var hallwayWidth = floor.Hallway?.Width ?? 0;
        var sideWidth = floor.Hallway is null
            ? building.Width
            : (building.Width - hallwayWidth) / 2;
        var sizes = floor.Rooms.Select(room => room.Size ?? building).ToArray();
        var columnDepths = new double[2];

        for (var index = 0; index < sizes.Length; index++)
        {
            Assert.True(sizes[index].Width <= sideWidth);
            columnDepths[floor.Hallway is null ? 0 : index % 2] += sizes[index].Depth;
        }

        Assert.All(columnDepths, depth => Assert.True(depth <= building.Depth));
    }
}
