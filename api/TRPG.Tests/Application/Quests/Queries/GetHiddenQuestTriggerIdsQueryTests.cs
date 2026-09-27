using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetHiddenQuestTriggerIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetHiddenQuestTriggerIdsQueryHandler _handler = null!;
    private readonly Guid _playerId = Guid.NewGuid();
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), worldId: WorldId);
    private readonly Trigger _trigger = Builders.MakeTrigger(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetHiddenQuestTriggerIdsQueryHandler>();

        _context.Quests.Add(_quest);
        _context.Props.Add(_trigger);
        _context.QuestObjectives.Add(
            Builders.MakeInteractWithPropObjective(_quest.Id, _trigger.Id, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_HidesTheTrigger_WhenPlayerHasNoRecordOfTheQuest()
    {
        // Act
        var result = await _handler.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                TriggerIds = [_trigger.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(_trigger.Id, result);
    }

    [Fact]
    public async Task Handle_ShowsTheTrigger_WhenPlayerHasAcceptedTheQuest()
    {
        // Arrange
        _context.CreatureQuests.Add(
            Builders.MakeCreatureQuest(_playerId, _quest.Id, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                TriggerIds = [_trigger.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData(QuestStatus.ReadyToComplete)]
    [InlineData(QuestStatus.Completed)]
    [InlineData(QuestStatus.Failed)]
    [InlineData(QuestStatus.Abandoned)]
    public async Task Handle_HidesTheTrigger_WhenPlayersQuestIsNoLongerAccepted(QuestStatus status)
    {
        // Arrange
        _context.CreatureQuests.Add(
            Builders.MakeCreatureQuest(_playerId, _quest.Id, status, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                TriggerIds = [_trigger.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(_trigger.Id, result);
    }

    [Fact]
    public async Task Handle_HidesTheTrigger_WhenOnlyAnotherCreatureAcceptedTheQuest()
    {
        // Arrange
        _context.CreatureQuests.Add(
            Builders.MakeCreatureQuest(Guid.NewGuid(), _quest.Id, worldId: WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                TriggerIds = [_trigger.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Contains(_trigger.Id, result);
    }

    [Fact]
    public async Task Handle_DoesNotHideATrigger_WhenNoQuestOwnsIt()
    {
        // Arrange
        var lever = Builders.MakeTrigger(WorldId);
        _context.Props.Add(lever);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = WorldId,
                PlayerId = _playerId,
                TriggerIds = [lever.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
