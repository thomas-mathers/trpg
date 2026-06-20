using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class QuestServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private QuestService _questService = null!;
    private Person _person = null!;
    private Person _giver = null!;
    private Quest _quest = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _questService = new QuestService(_context);

        _person = Builders.MakePerson();
        _giver = Builders.MakePerson();
        _quest = Builders.MakeQuest(giverId: _giver.Id);

        _context.Persons.Add(_person);
        _context.Persons.Add(_giver);
        _context.Quests.Add(_quest);

        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private async Task<Quest> SeedQuest(List<Guid>? prerequisiteQuestIds = null)
    {
        var quest = new Quest
        {
            GiverId = _giver.Id,
            Name = $"Quest-{Guid.NewGuid():N}",
            PrerequisiteQuestIds = prerequisiteQuestIds ?? []
        };
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync();
        return quest;
    }

    private async Task<QuestObjective> SeedObjective(Quest quest, QuestObjectiveType type = QuestObjectiveType.Kill, QuestTargetType targetType = QuestTargetType.Race)
    {
        var objective = new QuestObjective
        {
            QuestId = quest.Id,
            Name = $"Objective-{Guid.NewGuid():N}",
            Type = type,
            TargetType = targetType,
            Target = Guid.NewGuid(),
            Amount = 5,
            Region = new Circle { Center = new Location { WorldId = Guid.NewGuid(), Coordinates = new Point(0, 0) }, Radius = 100 }
        };
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync();
        return objective;
    }

    [Fact]
    public async Task AssignQuest_CreatesPersonQuestWithAcceptedStatus()
    {
        // Act
        await _questService.AssignQuest(_quest.Id, _person.Id);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(_person.Id);
        Assert.Single(personQuests);
        Assert.Equal(QuestStatus.Accepted, personQuests[0].Status);
    }

    [Fact]
    public async Task AssignQuest_Throws_WhenQuestDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _questService.AssignQuest(Guid.NewGuid(), _person.Id));
    }

    [Fact]
    public async Task AssignQuest_Throws_WhenPrerequisiteNotCompleted()
    {
        // Arrange
        var prereq = await SeedQuest();
        var locked = await SeedQuest(prerequisiteQuestIds: [prereq.Id]);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _questService.AssignQuest(locked.Id, _person.Id));
    }

    [Fact]
    public async Task AssignQuest_Succeeds_WhenPrerequisiteCompleted()
    {
        // Arrange
        var prereq = await SeedQuest();
        var locked = await SeedQuest(prerequisiteQuestIds: [prereq.Id]);
        await _questService.AssignQuest(prereq.Id, _person.Id);
        await _questService.SetQuestStatus(prereq.Id, _person.Id, QuestStatus.Completed);

        // Act
        await _questService.AssignQuest(locked.Id, _person.Id);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(_person.Id);
        Assert.Equal(2, personQuests.Count);
    }

    [Fact]
    public async Task AssignQuest_CreatesObjectivesForPerson()
    {
        // Arrange
        await SeedObjective(_quest, QuestObjectiveType.Kill, QuestTargetType.Race);
        await SeedObjective(_quest, QuestObjectiveType.Collect, QuestTargetType.Item);

        // Act
        await _questService.AssignQuest(_quest.Id, _person.Id);

        // Assert
        var objectives = await _questService.GetAllQuestObjectivesByPersonId(_person.Id);
        Assert.Equal(2, objectives.Count);
        Assert.All(objectives, o => Assert.Equal(0, o.Amount));
    }

    [Fact]
    public async Task ProgressObjective_IncrementsAmount()
    {
        // Arrange
        var objective = await SeedObjective(_quest);
        await _questService.AssignQuest(_quest.Id, _person.Id);

        // Act
        await _questService.ProgressObjective(_person.Id, objective.Id, 3);

        // Assert
        var objectives = await _questService.GetAllQuestObjectivesByPersonId(_person.Id);
        Assert.Equal(3, objectives[0].Amount);
    }

    [Fact]
    public async Task SetQuestStatus_UpdatesStatus()
    {
        // Arrange
        await _questService.AssignQuest(_quest.Id, _person.Id);

        // Act
        await _questService.SetQuestStatus(_quest.Id, _person.Id, QuestStatus.Completed);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(_person.Id);
        Assert.Equal(QuestStatus.Completed, personQuests[0].Status);
    }
}
