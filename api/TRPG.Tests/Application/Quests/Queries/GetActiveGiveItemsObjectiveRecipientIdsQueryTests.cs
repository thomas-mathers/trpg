using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetActiveGiveItemsObjectiveRecipientIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid GiverId = Guid.NewGuid();

    private readonly Guid _playerId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveGiveItemsObjectiveRecipientIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetActiveGiveItemsObjectiveRecipientIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedGiveItemsQuest(QuestStatus status, Guid playerId, Guid recipientId)
    {
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new GiveItemsObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            ItemIds = [Guid.NewGuid()],
            RecipientId = recipientId,
        };
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = playerId,
                QuestId = quest.Id,
                Status = status,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return recipientId;
    }

    [Fact]
    public async Task Handle_IncludesAcceptedAndReadyToCompleteRecipients()
    {
        // Arrange
        var acceptedRecipientId = await SeedGiveItemsQuest(
            QuestStatus.Accepted,
            _playerId,
            Guid.NewGuid()
        );
        var readyRecipientId = await SeedGiveItemsQuest(
            QuestStatus.ReadyToComplete,
            _playerId,
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(
            new GetActiveGiveItemsObjectiveRecipientIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { acceptedRecipientId, readyRecipientId }.OrderBy(id => id),
            result.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_IncludesGiveItemKindObjectiveRecipients()
    {
        // Arrange
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var recipientId = Guid.NewGuid();
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(
            Builders.MakeGiveItemKindObjective(
                quest.Id,
                "Goblin Ear",
                recipientId,
                worldId: WorldId
            )
        );
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _playerId,
                QuestId = quest.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetActiveGiveItemsObjectiveRecipientIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(recipientId, result);
    }

    [Fact]
    public async Task Handle_ExcludesCompletedRecipientsAndOtherPlayers()
    {
        // Arrange
        await SeedGiveItemsQuest(QuestStatus.Completed, _playerId, Guid.NewGuid());
        await SeedGiveItemsQuest(QuestStatus.Accepted, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            new GetActiveGiveItemsObjectiveRecipientIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
