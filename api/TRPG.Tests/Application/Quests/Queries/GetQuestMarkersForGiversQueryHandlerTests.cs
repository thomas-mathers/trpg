using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

[Collection("Database")]
public sealed class GetQuestMarkersForGiversQueryHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetQuestMarkersForGiversQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _availableGiver = Builders.MakeCreature(WorldId);
    private readonly Creature _readyGiver = Builders.MakeCreature(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetQuestMarkersForGiversQueryHandler>();

        _context.Creatures.AddRange(_player, _availableGiver, _readyGiver);
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
            new GetQuestMarkersForGiversQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                GiverIds = [_availableGiver.Id, _readyGiver.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(QuestMarker.Available, result[_availableGiver.Id]);
        Assert.Equal(QuestMarker.ReadyToTurnIn, result[_readyGiver.Id]);
    }
}
