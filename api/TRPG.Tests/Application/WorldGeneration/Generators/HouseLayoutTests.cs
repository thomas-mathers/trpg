using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class HouseLayoutTests
{
    public static TheoryData<int> HouseholdSizes =>
        new(Enumerable.Range(1, HouseBedroomPacker.MaximumHouseholdSize));

    [Theory]
    [MemberData(nameof(HouseholdSizes))]
    public void Generate_GivesEveryMemberABedInsideTheirBedroom(int householdSize)
    {
        // Arrange
        var memberIds = MakeMembers(householdSize);
        var world = MiniLayoutWorldBuilder.BuildHouseWorld(memberIds);

        // Act
        LocationLayoutGenerator.Generate(world.Input);

        // Assert
        var beds = world.Input.Props.OfType<Bed>().ToArray();
        Assert.Equal(memberIds.Order(), beds.Select(bed => bed.AssignedCreatureId!.Value).Order());
        Assert.All(
            beds,
            bed =>
            {
                var location = world.LocationById(bed.LocationId);
                Assert.True(BoxOf(bed).IsInside(location.Width, location.Depth));
            }
        );
    }

    [Theory]
    [MemberData(nameof(HouseholdSizes))]
    public void Generate_PlacesNoTwoBedroomSolidsOverlapping(int householdSize)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildHouseWorld(MakeMembers(householdSize));

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            world.Input.Rooms.Where(room =>
                room.Name.StartsWith("Bedroom", StringComparison.Ordinal)
            ),
            room =>
            {
                var boxes = world
                    .Input.Props.Concat(furniture)
                    .Where(prop => prop.LocationId == room.LocationId)
                    .Where(prop => prop is not Furniture { Model: PropModel.FurnitureRug })
                    .Select(BoxOf)
                    .ToArray();
                Assert.Empty(
                    boxes.SelectMany((box, index) => boxes.Skip(index + 1).Where(box.Overlaps))
                );
            }
        );
    }

    [Theory]
    [MemberData(nameof(HouseholdSizes))]
    public void Generate_LeavesEveryBedroomDoorAndStairApproachClear(int householdSize)
    {
        // Arrange
        var world = MiniLayoutWorldBuilder.BuildHouseWorld(MakeMembers(householdSize));

        // Act
        var furniture = LocationLayoutGenerator.Generate(world.Input);

        // Assert
        Assert.All(
            world.Input.Rooms.Where(room =>
                room.Name.StartsWith("Bedroom", StringComparison.Ordinal)
            ),
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
                var solids = world
                    .Input.Props.Concat(furniture)
                    .Where(prop => prop.LocationId == room.LocationId)
                    .Where(prop => prop is not Furniture { Model: PropModel.FurnitureRug })
                    .Select(BoxOf);
                Assert.DoesNotContain(solids, solid => keepOuts.Any(solid.Overlaps));
            }
        );
    }

    [Fact]
    public void GetSpecs_Throws_WhenTheHouseholdExceedsTheLargestHouse()
    {
        // Arrange
        var memberIds = MakeMembers(HouseBedroomPacker.MaximumHouseholdSize + 1);

        // Act
        var act = () =>
            BuildingSpecCatalog.GetSpecs(
                BuildingType.House,
                memberIds[0],
                memberIds,
                MiniLayoutWorldBuilder.HouseholdBedroomGroups(memberIds)
            );

        // Assert
        Assert.Throws<InvalidOperationException>(act);
    }

    private static Guid[] MakeMembers(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();

    private static OrientedBox BoxOf(Prop prop) =>
        OrientedBox.From(
            new Placement(prop.X, prop.Y, prop.Angle),
            new Footprint(prop.Width, prop.Depth)
        );
}
