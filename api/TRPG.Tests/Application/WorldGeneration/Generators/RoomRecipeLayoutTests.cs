using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class RoomRecipeLayoutTests
{
    public static TheoryData<BuildingType> RecipeBuildingTypes =>
        new(Enum.GetValues<BuildingType>().Where(type => !BuildingTypes.Dungeon.Contains(type)));

    public static TheoryData<BuildingType, int> StaffedBuildings =>
        new() { { BuildingType.Barracks, 7 }, { BuildingType.GuildHall, 6 } };

    [Theory]
    [MemberData(nameof(StaffedBuildings))]
    public void Generate_FurnishesEveryRoomWithoutOverlap_WhenTheBuildingIsFullyStaffed(
        BuildingType type,
        int memberCount
    )
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildBuildingWorld(
            type,
            Enumerable.Range(0, memberCount).Select(_ => Guid.NewGuid()).ToArray()
        );

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            RecipeRooms(world, type),
            room =>
            {
                var boxes = Solids(world, furniture, room).Select(BoxOf).ToArray();
                Assert.Empty(
                    boxes.SelectMany((box, index) => boxes.Skip(index + 1).Where(box.Overlaps))
                );
            }
        );
    }

    [Theory]
    [MemberData(nameof(RecipeBuildingTypes))]
    public void Generate_PlacesEverySpecPropInsideItsRoom_ForRecipeBuildings(BuildingType type)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            RecipeRooms(world, type),
            room =>
            {
                var location = world.LocationById(room.LocationId);
                var specProps = world.Input.Props.Where(prop => prop.LocationId == room.LocationId);
                Assert.All(
                    specProps,
                    prop =>
                    {
                        Assert.True(prop.Width > 0 && prop.Depth > 0);
                        Assert.True(BoxOf(prop).IsInside(location.Width, location.Depth));
                    }
                );
            }
        );
        Assert.NotEmpty(furniture);
    }

    [Theory]
    [MemberData(nameof(RecipeBuildingTypes))]
    public void Generate_PlacesNoTwoSolidsOverlapping_ForRecipeBuildings(BuildingType type)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            RecipeRooms(world, type),
            room =>
            {
                var boxes = Solids(world, furniture, room).Select(BoxOf).ToArray();
                Assert.Empty(
                    boxes.SelectMany((box, index) => boxes.Skip(index + 1).Where(box.Overlaps))
                );
            }
        );
    }

    [Theory]
    [MemberData(nameof(RecipeBuildingTypes))]
    public void Generate_LeavesEveryDoorAndStairApproachClear_ForRecipeBuildings(BuildingType type)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            RecipeRooms(world, type),
            room =>
            {
                var keepOuts = world
                    .Input.Connectors.Where(connector =>
                        connector.OriginLocationId == room.LocationId
                    )
                    .Select(connector =>
                        RoomFurnisher.KeepOut(
                            new ConnectorExit(
                                connector.Id,
                                new PlanarPoint(connector.ExitX, connector.ExitY),
                                connector.ExitAngle
                            )
                        )
                    )
                    .Select(rect => new OrientedBox(
                        rect.CenterX,
                        rect.CenterY,
                        rect.Width,
                        rect.Depth,
                        0
                    ))
                    .ToArray();
                var solids = Solids(world, furniture, room).Select(BoxOf).ToArray();
                Assert.DoesNotContain(solids, solid => keepOuts.Any(solid.Overlaps));
            }
        );
    }

    [Fact]
    public void Generate_PlacesTheInnFireplaceAndTables_InTheLobby()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var lobby = RecipeRooms(world, BuildingType.Inn).Single(room => room.Name == "Lobby");
        var models = furniture
            .OfType<Furniture>()
            .Where(item => item.LocationId == lobby.LocationId)
            .Select(item => item.Model)
            .ToArray();
        Assert.Contains(PropModel.FurnitureFireplace, models);
        Assert.Contains(PropModel.FurnitureTable, models);
    }

    [Fact]
    public void Generate_PlacesDisplayShelves_InTheBlacksmithWorkshop()
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var workshop = RecipeRooms(world, BuildingType.Blacksmith)
            .Single(room => room.Name == "Workshop");
        Assert.Contains(
            furniture.OfType<Furniture>(),
            item =>
                item.LocationId == workshop.LocationId
                && item.Model == PropModel.FurnitureDisplayShelf
        );
    }

    [Theory]
    [MemberData(nameof(RecipeBuildingTypes))]
    public void Generate_ProducesTheSameFurniture_WhenRunTwice(BuildingType type)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildWorld(1);
        var first = LocationLayoutGenerator.Generate(world.Input);

        // Act
        var second = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.Equal(Poses(first), Poses(second));
        Assert.NotEmpty(RecipeRooms(world, type));
    }

    private static Room[] RecipeRooms(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        BuildingType type
    )
    {
        var building = world.Input.Buildings.Single(candidate => candidate.BuildingType == type);

        return world
            .Input.Rooms.Where(room => room.BuildingId == building.Id)
            .Where(room => RoomRecipeCatalog.Find(type, room.Name) is not null)
            .ToArray();
    }

    private static Prop[] Solids(
        MiniLayoutWorldBuilder.MiniLayoutWorld world,
        IReadOnlyList<Prop> furniture,
        Room room
    ) =>
        world
            .Input.Props.Concat(furniture)
            .Where(prop => prop.LocationId == room.LocationId)
            .Where(prop => prop is not Furniture { Model: PropModel.FurnitureRug })
            .ToArray();

    private static double[] Poses(IReadOnlyList<Prop> furniture) =>
        furniture.SelectMany(prop => new[] { prop.X, prop.Y, prop.Angle, prop.Width }).ToArray();

    private static OrientedBox BoxOf(Prop prop) =>
        OrientedBox.From(
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );
}
