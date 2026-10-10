using TRPG.Application.WorldGeneration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class WorkstationAssignmentTests
{
    public static TheoryData<BuildingType> StandardStaffedBuildingTypes =>
        new(
            ShopBuildingTypes.All.Where(type =>
                type is not (BuildingType.Barracks or BuildingType.GuildHall)
            )
        );

    [Theory]
    [MemberData(nameof(StandardStaffedBuildingTypes))]
    public void StandardStaffedBuilding_GroundFloorHasAWorkstation(BuildingType type)
    {
        var spec = BuildingSpecCatalog.GetSpecs(type, Guid.NewGuid(), [], bedroomGroups: null);

        var workstations = spec
            .Rooms.Where(room => room.FloorNumber == 0)
            .SelectMany(room => room.Props)
            .Select(prop => prop.Factory(Guid.NewGuid(), Guid.NewGuid()))
            .OfType<Workstation>();

        Assert.NotEmpty(workstations);
    }

    [Fact]
    public void GuildHall_GroundFloorHasAWorkstation()
    {
        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.GuildHall,
            Guid.NewGuid(),
            [Guid.NewGuid()],
            bedroomGroups: null
        );

        var workstations = spec
            .Rooms.Where(room => room.FloorNumber == 0)
            .SelectMany(room => room.Props)
            .Select(prop => prop.Factory(Guid.NewGuid(), Guid.NewGuid()))
            .OfType<Workstation>();

        Assert.NotEmpty(workstations);
    }

    [Theory]
    [InlineData(BuildingType.Castle)]
    [InlineData(BuildingType.Jail)]
    public void GovernmentBuilding_GroundFloorHasAnUnassignedWorkstation(BuildingType type)
    {
        var spec = BuildingSpecCatalog.GetSpecs(type, Guid.NewGuid(), [], bedroomGroups: null);

        var workstations = spec
            .Rooms.Where(room => room.FloorNumber == 0)
            .SelectMany(room => room.Props)
            .Select(prop => prop.Factory(Guid.NewGuid(), Guid.NewGuid()))
            .OfType<Workstation>();

        Assert.NotEmpty(workstations);
    }
}
