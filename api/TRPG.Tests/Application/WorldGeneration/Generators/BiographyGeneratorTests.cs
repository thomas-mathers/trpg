using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class BiographyGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly State _state;

    public BiographyGeneratorTests()
    {
        _state = Builders.MakeState(Guid.NewGuid(), _worldId);
    }

    private IReadOnlyDictionary<Guid, Location> BirthLocations(Creature creature) =>
        new Dictionary<Guid, Location>
        {
            [creature.BirthLocationId] = new Location
            {
                Id = creature.BirthLocationId,
                Name = _state.Name,
                StateId = _state.Id,
                WorldId = _worldId,
                Kind = LocationKind.Wilderness,
            },
        };

    [Fact]
    public void AssignBiographies_IncludesWorkplaceAndHours_WhenCreatureHasAWorkJob()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId, birthYear: 950);
        var building = Builders.MakeBuilding(worldId: _worldId, name: "The Rising Crust");
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            startHour: 6,
            endHour: 14,
            locationId: room.LocationId,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                [workJob],
                [room],
                [building],
                []
            )
        );

        // Assert
        Assert.Contains(
            "They work at The Rising Crust, typically 6am to 2pm.",
            creature.Biography,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AssignBiographies_SaysTheyOwnTheWorkplace_WhenTheyOwnTheBuildingTheyWorkAt()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId, birthYear: 950);
        var building = Builders.MakeBuilding(worldId: _worldId, name: "The Stitched Hem");
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            startHour: 8,
            endHour: 18,
            locationId: room.LocationId,
            worldId: _worldId
        );
        var ownership = new BuildingOwner
        {
            BuildingId = building.Id,
            OwnerId = creature.Id,
            WorldId = _worldId,
        };

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                [workJob],
                [room],
                [building],
                [ownership]
            )
        );

        // Assert
        Assert.Contains(
            "They own The Stitched Hem, where they typically work 8am to 6pm.",
            creature.Biography,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("They work at", creature.Biography, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignBiographies_IncludesDaysOff_WhenCreatureHasASpecificDayJob()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId, birthYear: 950);
        var building = Builders.MakeBuilding(worldId: _worldId, name: "The Rising Crust");
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Work,
            startHour: 6,
            endHour: 14,
            locationId: room.LocationId,
            worldId: _worldId
        );
        var mondayOff = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Idle,
            specificDay: DayOfWeek.Monday,
            worldId: _worldId
        );
        var tuesdayOff = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Idle,
            specificDay: DayOfWeek.Tuesday,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                [workJob, mondayOff, tuesdayOff],
                [room],
                [building],
                []
            )
        );

        // Assert
        // GameClock's in-world day names, not .NET's - Monday and Tuesday are Ashday and Ironday
        Assert.Contains(
            "Their days off are Ashday and Ironday.",
            creature.Biography,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AssignBiographies_OmitsDaysOff_WhenCreatureIsUnemployed()
    {
        // Arrange - unemployed adults get a SpecificDay job for every weekday, but that's not a "day off"
        var creature = Builders.MakeCreature(
            _worldId,
            birthYear: 950,
            profession: Profession.Unemployed
        );
        var jobs = Enum.GetValues<DayOfWeek>()
            .Select(day =>
                Builders.MakeCreatureJob(
                    creature.Id,
                    action: CreatureJobAction.Idle,
                    specificDay: day,
                    worldId: _worldId
                )
            )
            .ToList();

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                jobs,
                [],
                [],
                []
            )
        );

        // Assert
        Assert.DoesNotContain("day off", creature.Biography, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignBiographies_IncludesHome_WhenCreatureHasASleepJob()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId, birthYear: 950);
        var building = Builders.MakeBuilding(worldId: _worldId, name: "Winterbough House");
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var sleepJob = Builders.MakeCreatureJob(
            creature.Id,
            action: CreatureJobAction.Sleep,
            startHour: 22,
            endHour: 6,
            locationId: room.LocationId,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                [sleepJob],
                [room],
                [building],
                []
            )
        );

        // Assert
        Assert.Contains(
            "They live at Winterbough House.",
            creature.Biography,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AssignBiographies_OmitsWorkplace_WhenCreatureHasNoWorkJob()
    {
        // Arrange
        var creature = Builders.MakeCreature(
            _worldId,
            birthYear: 950,
            profession: Profession.Unemployed
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                BirthLocations(creature),
                [],
                [],
                [],
                [],
                [],
                [],
                []
            )
        );

        // Assert
        Assert.DoesNotContain("They work at", creature.Biography, StringComparison.Ordinal);
        Assert.DoesNotContain("They live at", creature.Biography, StringComparison.Ordinal);
    }
}
