using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestMarkersForCreaturesQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetQuestMarkersForCreaturesQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _availableGiver = Builders.MakeCreature(WorldId);
    private readonly Creature _readyGiver = Builders.MakeCreature(WorldId);
    private readonly Creature _recipient = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetQuestMarkersForCreaturesQueryHandler>();

        _context.Creatures.AddRange(_player, _availableGiver, _readyGiver, _recipient);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsAvailableAndReadyToTurnInMarkers()
    {
        // Arrange
        var availableQuest = Builders.MakeQuest(_availableGiver.Id, WorldId);
        var readyQuest = Builders.MakeQuest(_readyGiver.Id, WorldId);
        _context.Quests.AddRange(availableQuest, readyQuest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = readyQuest.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestMarkersForCreaturesQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CreatureIds = [_availableGiver.Id, _readyGiver.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(QuestMarker.Available, result[_availableGiver.Id]);
        Assert.Equal(QuestMarker.ReadyToTurnIn, result[_readyGiver.Id]);
    }

    [Fact]
    public async Task Handle_ReturnsReadyToDeliverMarker_WhenPlayerHoldsAnUndeliveredItemForARecipient()
    {
        // Arrange
        var quest = Builders.MakeQuest(_availableGiver.Id, WorldId);
        var objective = new DeliverItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = Guid.NewGuid(),
            RecipientId = _recipient.Id,
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
            Builders.MakeCreatureQuestObjective(_player.Id, objective.Id, WorldId, amount: 0)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestMarkersForCreaturesQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CreatureIds = [_recipient.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(QuestMarker.ReadyToDeliver, result[_recipient.Id]);
    }

    [Fact]
    public async Task Handle_OmitsReadyToDeliverMarker_OnceTheItemHasAlreadyBeenDelivered()
    {
        // Arrange
        var quest = Builders.MakeQuest(_availableGiver.Id, WorldId);
        var objective = new DeliverItemObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemId = Guid.NewGuid(),
            RecipientId = _recipient.Id,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = quest.Id,
                Status = QuestStatus.ReadyToComplete,
                WorldId = WorldId,
            }
        );
        _context.CreatureQuestObjectives.Add(
            Builders.MakeCreatureQuestObjective(_player.Id, objective.Id, WorldId, amount: 1)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetQuestMarkersForCreaturesQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CreatureIds = [_recipient.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.ContainsKey(_recipient.Id));
    }
}
