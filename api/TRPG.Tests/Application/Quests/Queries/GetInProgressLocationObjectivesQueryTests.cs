using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

[Collection("Database")]
public sealed class GetInProgressLocationObjectivesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetInProgressLocationObjectivesQueryHandler _handler = null!;
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), worldId: WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetInProgressLocationObjectivesQueryHandler>();

        _context.Quests.Add(_quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyInProgressObjectivesWithATargetLocation()
    {
        // Arrange
        var inProgressObjective = Builders.MakeExploreLocationObjective(
            _quest.Id,
            worldId: WorldId,
            locationId: _locationId,
            requiredAmount: 3
        );
        var completedObjective = Builders.MakeExploreLocationObjective(
            _quest.Id,
            worldId: WorldId,
            locationId: _locationId,
            requiredAmount: 1
        );
        var locationlessObjective = Builders.MakeExploreLocationObjective(
            _quest.Id,
            worldId: WorldId,
            requiredAmount: 3
        );
        _context.QuestObjectives.AddRange(
            inProgressObjective,
            completedObjective,
            locationlessObjective
        );
        _context.CreatureQuestObjectives.AddRange(
            Builders.MakeCreatureQuestObjective(
                _playerId,
                inProgressObjective.Id,
                worldId: WorldId,
                amount: 1
            ),
            Builders.MakeCreatureQuestObjective(
                _playerId,
                completedObjective.Id,
                worldId: WorldId,
                amount: 1
            ),
            Builders.MakeCreatureQuestObjective(
                _playerId,
                locationlessObjective.Id,
                worldId: WorldId,
                amount: 1
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetInProgressLocationObjectivesQuery { PlayerId = _playerId, WorldId = WorldId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var objective = Assert.Single(result);
        Assert.Equal(_quest.Id, objective.QuestId);
        Assert.Equal(_locationId, objective.LocationId);
    }
}
