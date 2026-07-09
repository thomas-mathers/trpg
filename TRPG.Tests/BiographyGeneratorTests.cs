using TRPG.Generators;
using TRPG.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public class BiographyGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly State _state;

    public BiographyGeneratorTests()
    {
        _state = Builders.MakeState(Guid.NewGuid(), _worldId);
    }

    [Fact]
    public void AssignBiographies_IncludesWorkplaceAndHours_WhenCreatureHasAWorkJob()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        var building = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            name: "The Rising Crust"
        );
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeJob(
            creature.Id,
            action: JobAction.Work,
            startHour: 6,
            endHour: 14,
            roomId: room.Id,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
        var creature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        var building = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            name: "The Stitched Hem"
        );
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeJob(
            creature.Id,
            action: JobAction.Work,
            startHour: 8,
            endHour: 18,
            roomId: room.Id,
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
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
        var creature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        var building = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            name: "The Rising Crust"
        );
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var workJob = Builders.MakeJob(
            creature.Id,
            action: JobAction.Work,
            startHour: 6,
            endHour: 14,
            roomId: room.Id,
            worldId: _worldId
        );
        var mondayOff = Builders.MakeJob(
            creature.Id,
            action: JobAction.Sit,
            specificDay: DayOfWeek.Monday,
            worldId: _worldId
        );
        var tuesdayOff = Builders.MakeJob(
            creature.Id,
            action: JobAction.Sit,
            specificDay: DayOfWeek.Tuesday,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
        // GameClock's in-world day names, not .NET's — Monday and Tuesday are Ashday and Ironday
        Assert.Contains(
            "Their days off are Ashday and Ironday.",
            creature.Biography,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void AssignBiographies_OmitsDaysOff_WhenCreatureIsUnemployed()
    {
        // Arrange — unemployed adults get a SpecificDay job for every weekday, but that's not a "day off"
        var creature = Builders.MakeCreature(
            _worldId,
            birthStateId: _state.Id,
            birthYear: 950,
            profession: Profession.Unemployed
        );
        var jobs = Enum.GetValues<DayOfWeek>()
            .Select(day =>
                Builders.MakeJob(
                    creature.Id,
                    action: JobAction.Sit,
                    specificDay: day,
                    worldId: _worldId
                )
            )
            .ToList();

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
        var creature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        var building = Builders.MakeBuilding(
            _state.Id,
            worldId: _worldId,
            name: "Winterbough House"
        );
        var room = Builders.MakeRoom(building.Id, worldId: _worldId);
        var sleepJob = Builders.MakeJob(
            creature.Id,
            action: JobAction.Sleep,
            startHour: 22,
            endHour: 6,
            roomId: room.Id,
            worldId: _worldId
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
            birthStateId: _state.Id,
            birthYear: 950,
            profession: Profession.Unemployed
        );

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [creature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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

    [Fact]
    public void AssignBiographies_NeverMentionsGold()
    {
        // Arrange — one rich, one poor, so the removed gold-outlier sentence would have fired for both
        var richCreature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        richCreature.Gold = 100_000;
        var poorCreature = Builders.MakeCreature(_worldId, birthStateId: _state.Id, birthYear: 950);
        poorCreature.Gold = 0;

        // Act
        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                [richCreature, poorCreature],
                new Dictionary<Guid, State> { [_state.Id] = _state },
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
        Assert.DoesNotContain("wealthy", richCreature.Biography, StringComparison.Ordinal);
        Assert.DoesNotContain("scrape by", poorCreature.Biography, StringComparison.Ordinal);
    }
}
