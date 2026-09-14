using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetActiveClearLocationObjectiveBuildingIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid GiverId = Guid.NewGuid();

    private readonly Guid _playerId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetActiveClearLocationObjectiveBuildingIdsQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetActiveClearLocationObjectiveBuildingIdsQueryHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedClearQuest(QuestStatus status, Guid playerId, Guid buildingId)
    {
        var quest = Builders.MakeQuest(GiverId, WorldId);
        var objective = new ClearLocationObjective
        {
            WorldId = WorldId,
            QuestId = quest.Id,
            BuildingId = buildingId,
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
        return buildingId;
    }

    [Fact]
    public async Task Handle_IncludesAcceptedAndReadyToCompleteBuildings()
    {
        // Arrange
        var acceptedBuildingId = await SeedClearQuest(
            QuestStatus.Accepted,
            _playerId,
            Guid.NewGuid()
        );
        var readyBuildingId = await SeedClearQuest(
            QuestStatus.ReadyToComplete,
            _playerId,
            Guid.NewGuid()
        );

        // Act
        var result = await _handler.Handle(
            new GetActiveClearLocationObjectiveBuildingIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { acceptedBuildingId, readyBuildingId }.OrderBy(id => id),
            result.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_ExcludesCompletedBuildingsAndOtherPlayers()
    {
        // Arrange
        await SeedClearQuest(QuestStatus.Completed, _playerId, Guid.NewGuid());
        await SeedClearQuest(QuestStatus.Accepted, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            new GetActiveClearLocationObjectiveBuildingIdsQuery
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
