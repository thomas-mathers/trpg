using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class QuestServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private PersonService _personService = null!;
    private QuestService _questService = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _personService = new PersonService(_context);
        _questService = new QuestService(_context);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    private async Task<(Person person, Quest quest)> SeedPersonAndQuest()
    {
        var person = Builders.MakePerson();
        await _personService.Add(person);

        var giver = Builders.MakePerson();
        await _personService.Add(giver);

        var quest = Builders.MakeQuest(giverId: giver.Id);
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync();

        return (person, quest);
    }

    private static Circle MakeRegion() =>
        new() { Center = new Location { WorldId = Guid.NewGuid(), Coordinates = new Point(0, 0) }, Radius = 100 };

    [Fact]
    public async Task AssignQuest_CreatesPersonQuestWithAcceptedStatus()
    {
        // Arrange
        var (person, quest) = await SeedPersonAndQuest();

        // Act
        await _questService.AssignQuest(quest.Id, person.Id);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(person.Id);
        Assert.Single(personQuests);
        Assert.Equal(QuestStatus.Accepted, personQuests[0].Status);
    }

    [Fact]
    public async Task AssignQuest_Throws_WhenQuestDoesNotExist()
    {
        // Arrange
        var (person, _) = await SeedPersonAndQuest();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _questService.AssignQuest(Guid.NewGuid(), person.Id));
    }

    [Fact]
    public async Task AssignQuest_Throws_WhenPrerequisiteNotCompleted()
    {
        // Arrange
        var giver = Builders.MakePerson();
        await _personService.Add(giver);

        var person = Builders.MakePerson();
        await _personService.Add(person);

        var prereqQuest = Builders.MakeQuest(giverId: giver.Id);
        _context.Quests.Add(prereqQuest);
        await _context.SaveChangesAsync();

        var quest = new Quest
        {
            GiverId = giver.Id,
            Name = $"Locked-{Guid.NewGuid():N}",
            PrerequisiteQuestIds = [prereqQuest.Id]
        };
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _questService.AssignQuest(quest.Id, person.Id));
    }

    [Fact]
    public async Task AssignQuest_Succeeds_WhenPrerequisiteCompleted()
    {
        // Arrange
        var giver = Builders.MakePerson();
        await _personService.Add(giver);

        var person = Builders.MakePerson();
        await _personService.Add(person);

        var prereqQuest = Builders.MakeQuest(giverId: giver.Id);
        _context.Quests.Add(prereqQuest);
        await _context.SaveChangesAsync();

        var quest = new Quest
        {
            GiverId = giver.Id,
            Name = $"Locked-{Guid.NewGuid():N}",
            PrerequisiteQuestIds = [prereqQuest.Id]
        };
        _context.Quests.Add(quest);
        await _context.SaveChangesAsync();

        await _questService.AssignQuest(prereqQuest.Id, person.Id);
        await _questService.SetQuestStatus(prereqQuest.Id, person.Id, QuestStatus.Completed);

        // Act
        await _questService.AssignQuest(quest.Id, person.Id);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(person.Id);
        Assert.Equal(2, personQuests.Count);
    }

    [Fact]
    public async Task AssignQuest_CreatesObjectivesForPerson()
    {
        // Arrange
        var (person, quest) = await SeedPersonAndQuest();

        _context.QuestObjectives.AddRange(
            new QuestObjective { QuestId = quest.Id, Name = "Kill wolves", Type = QuestObjectiveType.Kill, TargetType = QuestTargetType.Race, Target = Guid.NewGuid(), Amount = 5, Region = MakeRegion() },
            new QuestObjective { QuestId = quest.Id, Name = "Collect pelts", Type = QuestObjectiveType.Collect, TargetType = QuestTargetType.Item, Target = Guid.NewGuid(), Amount = 3, Region = MakeRegion() }
        );
        await _context.SaveChangesAsync();

        // Act
        await _questService.AssignQuest(quest.Id, person.Id);

        // Assert
        var objectives = await _questService.GetAllQuestObjectivesByPersonId(person.Id);
        Assert.Equal(2, objectives.Count);
        Assert.All(objectives, o => Assert.Equal(0, o.Amount));
    }

    [Fact]
    public async Task ProgressObjective_IncrementsAmount()
    {
        // Arrange
        var (person, quest) = await SeedPersonAndQuest();

        var objective = new QuestObjective { QuestId = quest.Id, Name = "Kill wolves", Type = QuestObjectiveType.Kill, TargetType = QuestTargetType.Race, Target = Guid.NewGuid(), Amount = 5, Region = MakeRegion() };
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync();

        await _questService.AssignQuest(quest.Id, person.Id);

        // Act
        await _questService.ProgressObjective(person.Id, objective.Id, 3);

        // Assert
        var objectives = await _questService.GetAllQuestObjectivesByPersonId(person.Id);
        Assert.Equal(3, objectives[0].Amount);
    }

    [Fact]
    public async Task SetQuestStatus_UpdatesStatus()
    {
        // Arrange
        var (person, quest) = await SeedPersonAndQuest();
        await _questService.AssignQuest(quest.Id, person.Id);

        // Act
        await _questService.SetQuestStatus(quest.Id, person.Id, QuestStatus.Completed);

        // Assert
        var personQuests = await _questService.GetAllQuestsByPersonId(person.Id);
        Assert.Equal(QuestStatus.Completed, personQuests[0].Status);
    }
}
