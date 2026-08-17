using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.GameTurns;
using TRPG.Application.Quests.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Quests.Tools;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Tools;

[Collection("Database")]
public sealed class ShowQuestDetailsToolTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ShowQuestDetailsTool _tool = null!;
    private TestGameClientEventSink _events = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Creature _giver = Builders.MakeCreature(
        WorldId,
        locationId: LocationId,
        name: "Quest Giver"
    );
    private Quest _quest = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _tool = _serviceProvider.GetRequiredService<ShowQuestDetailsTool>();
        _events = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        var turnContext = _serviceProvider.GetRequiredService<GameTurnContext>();
        turnContext.PlayerId = _player.Id;
        turnContext.WorldId = WorldId;
        _quest = Builders.MakeQuest(_giver.Id, WorldId);
        _context.Creatures.AddRange(_player, _giver);
        _context.Quests.Add(_quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Invoke_PublishesOfferDialog_WhenQuestIsAvailableFromNpc()
    {
        // Arrange
        var objective = new ExploreLocationObjective
        {
            LocationId = Guid.NewGuid(),
            QuestId = _quest.Id,
            WorldId = WorldId,
            Name = "Visit the city",
            Description = "Visit the city.",
        };
        _context.QuestObjectives.Add(objective);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var invoke = (Func<string, string, CancellationToken, Task<object?>>)_tool.Invoke;

        // Act
        await invoke(_giver.Name, _quest.Name, TestContext.Current.CancellationToken);

        // Assert
        var gameEvent = Assert.IsType<QuestDialogRequestedEvent>(
            Assert.Single(_events.EnqueuedEvents)
        );
        Assert.Equal(QuestDialogMode.Offer, gameEvent.Mode);
        Assert.Equal(WorldId, gameEvent.WorldId);
        Assert.Equal(_quest.Id, gameEvent.Quest.QuestId);
        Assert.Equal(_quest.Name, gameEvent.Quest.Name);
        Assert.Equal(objective.Name, Assert.Single(gameEvent.Quest.Objectives).Name);
    }
}
