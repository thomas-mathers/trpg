using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetDeliverableItemForRecipientQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetDeliverableItemForRecipientQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Creature _recipient = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetDeliverableItemForRecipientQueryHandler>();

        _context.Creatures.AddRange(_player, _giver, _recipient);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<DeliverItemObjective> SeedDeliverItemQuest(int amount)
    {
        var quest = Builders.MakeQuest(_giver.Id, WorldId);
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
            Builders.MakeCreatureQuestObjective(_player.Id, objective.Id, WorldId, amount)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return objective;
    }

    [Fact]
    public async Task Handle_ReturnsTheItem_WhenAnUndeliveredObjectiveTargetsTheRecipient()
    {
        // Arrange
        var objective = await SeedDeliverItemQuest(amount: 0);

        // Act
        var result = await _handler.Handle(
            new GetDeliverableItemForRecipientQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                RecipientId = _recipient.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(objective.ItemId, result.ItemId);
        Assert.Equal(objective.QuestId, result.QuestId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheItemHasAlreadyBeenDelivered()
    {
        // Arrange
        await SeedDeliverItemQuest(amount: 1);

        // Act
        var result = await _handler.Handle(
            new GetDeliverableItemForRecipientQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                RecipientId = _recipient.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoObjectiveTargetsThatRecipient()
    {
        // Act
        var result = await _handler.Handle(
            new GetDeliverableItemForRecipientQuery
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                RecipientId = _recipient.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
