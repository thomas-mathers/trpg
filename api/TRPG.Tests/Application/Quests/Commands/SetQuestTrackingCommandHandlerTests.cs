using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Quests.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Commands;

[Collection("Database")]
public sealed class SetQuestTrackingCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SetQuestTrackingCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private readonly Creature _giver = Builders.MakeCreature(WorldId);
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SetQuestTrackingCommandHandler>();
        _context.Creatures.AddRange(_player, _giver);
        _context.Quests.Add(_quest);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = _player.Id,
                QuestId = _quest.Id,
                Status = QuestStatus.Accepted,
                IsTracked = true,
                WorldId = WorldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesTracking_WhenQuestIsAccepted()
    {
        // Arrange
        var command = new SetQuestTrackingCommand
        {
            PlayerId = _player.Id,
            QuestId = _quest.Id,
            WorldId = WorldId,
            IsTracked = false,
        };

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _context.ChangeTracker.Clear();
        var quest = await _context.CreatureQuests.SingleAsync(
            quest => quest.CreatureId == _player.Id && quest.QuestId == _quest.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(quest.IsTracked);
    }
}
