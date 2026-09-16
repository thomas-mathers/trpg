using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestJournalQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private GetQuestJournalQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<GetQuestJournalQueryHandler>();
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = _quest.Id,
            Name = "Collect",
            Description = "Collect",
            ItemId = Guid.NewGuid(),
            RequiredAmount = 3,
        };
        _context.Creatures.Add(_player);
        _context.Quests.Add(_quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = _quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = false,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 2,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsQuestTrackingAndObjectiveProgress()
    {
        // Arrange
        var query = new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId };

        // Act
        var journal = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        var quest = Assert.Single(journal);
        var objective = Assert.Single(quest.Objectives);
        Assert.False(quest.IsTracked);
        Assert.Equal(2, objective.Amount);
        Assert.Equal(3, objective.RequiredAmount);
        Assert.Null(objective.Items);
    }

    [Fact]
    public async Task Handle_BreaksDownGiveItemsObjectiveProgress_ByItemName()
    {
        // Arrange
        var firstGear = Builders.MakeItem(WorldId, "Construct Gear");
        firstGear.Ownership.OwnerId = _player.Id;
        firstGear.Ownership.OwnerType = OwnerType.Creature;
        var secondGear = Builders.MakeItem(WorldId, "Construct Gear");
        secondGear.Ownership.OwnerId = _player.Id;
        secondGear.Ownership.OwnerType = OwnerType.Creature;
        var goblinEar = Builders.MakeItem(WorldId, "Goblin Ear");
        goblinEar.Ownership.OwnerId = Guid.NewGuid();
        goblinEar.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.AddRange(firstGear, secondGear, goblinEar);

        var quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);
        var objective = new GiveItemsObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            Name = "Gather materials",
            Description = "Gather materials",
            ItemIds = [firstGear.Id, secondGear.Id, goblinEar.Id],
            RecipientId = Guid.NewGuid(),
            RequiredAmount = 3,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 2,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var journal = await _handler.Handle(
            new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var journalEntry = Assert.Single(journal, entry => entry.Id == quest.Id);
        var progress = Assert.Single(journalEntry.Objectives);
        Assert.NotNull(progress.Items);
        var gearProgress = Assert.Single(progress.Items, item => item.Name == "Construct Gear");
        Assert.Equal(2, gearProgress.Amount);
        Assert.Equal(2, gearProgress.RequiredAmount);
        var earProgress = Assert.Single(progress.Items, item => item.Name == "Goblin Ear");
        Assert.Equal(0, earProgress.Amount);
        Assert.Equal(1, earProgress.RequiredAmount);
    }

    [Fact]
    public async Task Handle_ReportsRemainingLocations_ForUnownedGiveItemsAtTheirCurrentOwnerLocation()
    {
        // Arrange — two still-uncollected items scattered across two locations, plus one item the
        // player already has, proving location is resolved live from the item's current owner and
        // only reported for items that are still outstanding.
        var locationA = Builders.MakeLocation(WorldId, Guid.NewGuid(), name: "Location A");
        var locationB = Builders.MakeLocation(WorldId, Guid.NewGuid(), name: "Location B");
        var ownerA = Builders.MakeCreature(WorldId, locationId: locationA.Id);
        var ownerB = Builders.MakeCreature(WorldId, locationId: locationB.Id);
        _context.Locations.AddRange(locationA, locationB);
        _context.Creatures.AddRange(ownerA, ownerB);

        var itemAtA = Builders.MakeItem(WorldId, "Beast Pelt");
        itemAtA.Ownership.OwnerId = ownerA.Id;
        itemAtA.Ownership.OwnerType = OwnerType.Creature;
        var itemAtB = Builders.MakeItem(WorldId, "Beast Pelt");
        itemAtB.Ownership.OwnerId = ownerB.Id;
        itemAtB.Ownership.OwnerType = OwnerType.Creature;
        var ownedItem = Builders.MakeItem(WorldId, "Beast Pelt");
        ownedItem.Ownership.OwnerId = _player.Id;
        ownedItem.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.AddRange(itemAtA, itemAtB, ownedItem);

        var quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);
        var objective = new GiveItemsObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            Name = "Recover 3x Beast Pelt",
            Description = "Recover 3x Beast Pelt",
            ItemIds = [itemAtA.Id, itemAtB.Id, ownedItem.Id],
            RecipientId = Guid.NewGuid(),
            RequiredAmount = 3,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 1,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var journal = await _handler.Handle(
            new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var progress = Assert.Single(
            Assert.Single(journal, entry => entry.Id == quest.Id).Objectives
        );
        Assert.NotNull(progress.RemainingLocations);
        Assert.Equal(2, progress.RemainingLocations.Count);
        Assert.Contains(
            progress.RemainingLocations,
            location => location.LocationName == locationA.Name && location.RemainingCount == 1
        );
        Assert.Contains(
            progress.RemainingLocations,
            location => location.LocationName == locationB.Name && location.RemainingCount == 1
        );
    }

    [Fact]
    public async Task Handle_ReflectsAnOwningCreaturesCurrentLocation_NotItsLocationWhenSeeded()
    {
        // Arrange
        var originalLocation = Builders.MakeLocation(WorldId, Guid.NewGuid(), name: "Old Spot");
        var newLocation = Builders.MakeLocation(WorldId, Guid.NewGuid(), name: "New Spot");
        var owner = Builders.MakeCreature(WorldId, locationId: originalLocation.Id);
        _context.Locations.AddRange(originalLocation, newLocation);
        _context.Creatures.Add(owner);

        var item = Builders.MakeItem(WorldId, "Pocket Watch");
        item.Ownership.OwnerId = owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);

        var quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);
        var objective = new GiveItemsObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            Name = "Recover Pocket Watch",
            Description = "Recover Pocket Watch",
            ItemIds = [item.Id],
            RecipientId = Guid.NewGuid(),
            RequiredAmount = 1,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            new CreatureQuestObjective
            {
                CreatureId = _player.Id,
                ObjectiveId = objective.Id,
                Amount = 0,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        owner.LocationId = newLocation.Id;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var journal = await _handler.Handle(
            new GetQuestJournalQuery { PlayerId = _player.Id, WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var progress = Assert.Single(
            Assert.Single(journal, entry => entry.Id == quest.Id).Objectives
        );
        var remainingLocation = Assert.Single(progress.RemainingLocations!);
        Assert.Equal(newLocation.Name, remainingLocation.LocationName);
    }
}
