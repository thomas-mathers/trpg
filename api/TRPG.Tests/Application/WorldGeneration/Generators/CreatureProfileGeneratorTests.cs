using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreatureProfileGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Generate_ListsDaysOffWithInWorldDayNames_WhenCreatureHasSpecificDayJobs()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);
        var building = Builders.MakeBuilding(worldId: _worldId, name: "The Rising Crust");
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            locationId: room.LocationId,
            worldId: _worldId
        );
        var sundayOff = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Idle,
            specificDay: DayOfWeek.Sunday,
            worldId: _worldId
        );
        var mondayOff = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Idle,
            specificDay: DayOfWeek.Monday,
            worldId: _worldId
        );
        var birthLocation = new Location
        {
            Id = creature.BirthLocationId,
            Name = "Birthplace",
            StateId = Guid.NewGuid(),
            WorldId = _worldId,
            Kind = LocationKind.Wilderness,
        };

        // Act
        var profiles = CreatureProfileGenerator.Generate(
            new CreatureProfileGeneratorInput(
                [creature],
                new Dictionary<Guid, Location> { [birthLocation.Id] = birthLocation },
                [],
                [],
                [],
                [workJob, sundayOff, mondayOff],
                [room],
                [building],
                []
            )
        );

        // Assert
        var profile = Assert.Single(profiles);
        Assert.Equal(["Emberday", "Ashday"], profile.PrivateBackground.Work!.DaysOff);
    }
}
