using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetActiveFreeCreatureObjectiveCreatureIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid GiverId = Guid.NewGuid();

    private readonly Guid _playerId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveFreeCreatureObjectiveCreatureIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetActiveFreeCreatureObjectiveCreatureIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedFreeCreatureQuest(QuestStatus status, Guid playerId)
    {
        var captiveId = Guid.NewGuid();
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new FreeCreatureObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            CreatureId = captiveId,
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
        return captiveId;
    }

    [Fact]
    public async Task Handle_IncludesAcceptedAndReadyToCompleteCaptiveIds()
    {
        // Arrange
        var acceptedCaptiveId = await SeedFreeCreatureQuest(QuestStatus.Accepted, _playerId);
        var readyCaptiveId = await SeedFreeCreatureQuest(QuestStatus.ReadyToComplete, _playerId);

        // Act
        var result = await _handler.Handle(
            new GetActiveFreeCreatureObjectiveCreatureIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { acceptedCaptiveId, readyCaptiveId }.OrderBy(id => id),
            result.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_ExcludesCompletedCaptivesAndOtherPlayers()
    {
        // Arrange
        await SeedFreeCreatureQuest(QuestStatus.Completed, _playerId);
        await SeedFreeCreatureQuest(QuestStatus.Accepted, Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            new GetActiveFreeCreatureObjectiveCreatureIdsQuery
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
