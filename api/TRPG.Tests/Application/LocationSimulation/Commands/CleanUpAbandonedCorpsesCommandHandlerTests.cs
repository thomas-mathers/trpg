using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

[Collection("Database")]
public sealed class CleanUpAbandonedCorpsesCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _stateId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CleanUpAbandonedCorpsesCommandHandler _handler = null!;
    private Location _location = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CleanUpAbandonedCorpsesCommandHandler>();

        var state = Builders.MakeState(Guid.NewGuid(), worldId: WorldId, id: _stateId);
        _location = Builders.MakeLocation(WorldId, _stateId);
        _player = Builders.MakeCreature(WorldId, locationId: _location.Id);
        _context.States.Add(state);
        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DeletesDeadCreaturesAtTheLocation()
    {
        // Arrange
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Dead
        );
        _context.Creatures.Add(corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CleanUpAbandonedCorpsesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _location.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remainingCorpse = await verifyContext.Creatures.FindAsync(
            [corpse.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Null(remainingCorpse);
    }

    [Fact]
    public async Task Handle_KeepsCorpse_WhenItHoldsAnActiveQuestItem()
    {
        // Arrange
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Dead
        );
        var quest = Builders.MakeQuest(corpse.Id, WorldId);
        var item = Builders.MakeWeapon(WorldId);
        item.Ownership.OwnerId = corpse.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        var objective = new CollectItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = item.Id,
        };
        _context.Creatures.Add(corpse);
        _context.Quests.Add(quest);
        _context.Items.Add(item);
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
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CleanUpAbandonedCorpsesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _location.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.NotNull(
            await verifyContext.Creatures.FindAsync(
                [corpse.Id],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_KeepsCorpse_WhenPlayerCorpseStillHoldsItems()
    {
        // Arrange
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Dead,
            playerCorpseOwnerId: _player.Id
        );
        var item = Builders.MakeWeapon(WorldId, quantity: 1);
        item.Ownership.OwnerId = corpse.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Creatures.Add(corpse);
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CleanUpAbandonedCorpsesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _location.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.NotNull(
            await verifyContext.Creatures.FindAsync(
                [corpse.Id],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_RemovesCorpse_WhenPlayerCorpseHasBeenFullyLooted()
    {
        // Arrange
        var corpse = Builders.MakeCreature(
            WorldId,
            locationId: _location.Id,
            state: CreatureState.Dead,
            playerCorpseOwnerId: _player.Id
        );
        _context.Creatures.Add(corpse);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new CleanUpAbandonedCorpsesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = _location.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.Null(
            await verifyContext.Creatures.FindAsync(
                [corpse.Id],
                TestContext.Current.CancellationToken
            )
        );
    }
}
