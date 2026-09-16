using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetActiveKillCreatureObjectiveCreatureIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid GiverId = Guid.NewGuid();

    private readonly Guid _playerId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveKillCreatureObjectiveCreatureIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetActiveKillCreatureObjectiveCreatureIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedKillQuest(QuestStatus status, Guid playerId, Guid creatureId)
    {
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new KillCreatureObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            CreatureId = creatureId,
            RequiredAmount = 1,
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
        return creatureId;
    }

    [Fact]
    public async Task Handle_IncludesAcceptedAndReadyToCompleteCreatures()
    {
        // Arrange
        var acceptedCreatureId = await SeedKillQuest(
            QuestStatus.Accepted,
            _playerId,
            Guid.NewGuid()
        );
        var readyCreatureId = await SeedKillQuest(
            QuestStatus.ReadyToComplete,
            _playerId,
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(
            new GetActiveKillCreatureObjectiveCreatureIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { acceptedCreatureId, readyCreatureId }.OrderBy(id => id),
            result.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_ExcludesCompletedCreaturesAndOtherPlayers()
    {
        // Arrange
        await SeedKillQuest(QuestStatus.Completed, _playerId, Guid.NewGuid());
        await SeedKillQuest(QuestStatus.Accepted, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            new GetActiveKillCreatureObjectiveCreatureIdsQuery
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
