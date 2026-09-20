using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class ActivateTriggerCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid PlayerId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ActivateTriggerCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ActivateTriggerCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MarksTheTriggerActivated_AndPersistsIt()
    {
        // Arrange
        var trigger = Builders.MakeTrigger();
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.True(updated.IsActivated);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyActivatedFalse_OnTheFirstActivation()
    {
        // Arrange
        var trigger = Builders.MakeTrigger();
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.AlreadyActivated);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyActivatedTrue_WhenActivatedASecondTime()
    {
        // Arrange
        var trigger = Builders.MakeTrigger(isActivated: true);
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id, PlayerId = PlayerId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.AlreadyActivated);
    }

    [Fact]
    public async Task Handle_AdvancesAMatchingInteractWithPropObjectiveOnce_EvenWhenActivatedTwice()
    {
        // Arrange
        var trigger = Builders.MakeTrigger();
        var giver = Builders.MakeCreature(trigger.WorldId);
        var quest = Builders.MakeQuest(giver.Id, trigger.WorldId);
        var objective = new InteractWithPropObjective
        {
            WorldId = trigger.WorldId,
            QuestId = quest.Id,
            TriggerId = trigger.Id,
        };
        var progress = new CreatureQuestObjective
        {
            CreatureId = PlayerId,
            ObjectiveId = objective.Id,
            WorldId = trigger.WorldId,
        };
        var creatureQuest = new CreatureQuest
        {
            CreatureId = PlayerId,
            QuestId = quest.Id,
            Status = QuestStatus.Accepted,
            WorldId = trigger.WorldId,
        };
        _context.Props.Add(trigger);
        _context.Creatures.Add(giver);
        _context.Quests.Add(quest);
        _context.QuestObjectives.Add(objective);
        _context.CreatureQuestObjectives.Add(progress);
        _context.CreatureQuests.Add(creatureQuest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var command = new ActivateTriggerCommand { TriggerId = trigger.Id, PlayerId = PlayerId };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var updatedProgress = await _context.CreatureQuestObjectives.SingleAsync(
            creatureQuestObjective => creatureQuestObjective.Id == progress.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, updatedProgress.Amount);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFoundException_WhenTheTriggerDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                new ActivateTriggerCommand { TriggerId = Guid.NewGuid(), PlayerId = PlayerId },
                TestContext.Current.CancellationToken
            )
        );
    }
}
