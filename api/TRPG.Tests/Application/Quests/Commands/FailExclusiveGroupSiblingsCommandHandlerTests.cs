using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

public sealed class FailExclusiveGroupSiblingsCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly RecordingDomainEventConsumer<QuestMarkersChangedEvent> _markersChanged = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private FailExclusiveGroupSiblingsCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IDomainEventConsumer<QuestMarkersChangedEvent>>(_markersChanged)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<FailExclusiveGroupSiblingsCommandHandler>();
        _context.Creatures.AddRange(_player, _giver);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PublishesQuestMarkersChanged_WhenASiblingQuestFails()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var completed = MakeGroupedQuest(groupId);
        var sibling = MakeGroupedQuest(groupId);
        _context.Quests.AddRange(completed, sibling);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = sibling.Id,
                Status = QuestStatus.Accepted,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new FailExclusiveGroupSiblingsCommand
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                CompletedQuestId = completed.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new QuestMarkersChangedEvent(_player.Id, WorldId),
            Assert.Single(_markersChanged.Events)
        );
    }

    private Quest MakeGroupedQuest(Guid groupId) =>
        new()
        {
            WorldId = WorldId,
            GiverId = _giver.Id,
            Name = $"Quest-{Guid.NewGuid():N}",
            Description = "A test quest",
            GoldReward = 100,
            ExclusiveGroupId = groupId,
        };
}
