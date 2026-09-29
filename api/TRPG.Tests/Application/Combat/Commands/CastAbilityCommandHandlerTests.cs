using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Commands;

public sealed class CastAbilityCommandHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private const string Mend = "Mend";

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private CastAbilityCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<CastAbilityCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedPlayer(bool isEngaged = false, int currentMp = 8)
    {
        var player = Builders.MakeCreature(
            _worldId,
            locationId: _locationId,
            name: "Hero",
            currentHp: 20,
            currentMp: currentMp,
            isEngaged: isEngaged
        );
        _context.Creatures.Add(player);
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(
                player.Id,
                Skill.Restoration,
                level: 1,
                experience: 10_000,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return player;
    }

    private async Task<Creature> SeedBystander(Guid? locationId = null, int currentHp = 10)
    {
        var bystander = Builders.MakeCreature(
            _worldId,
            locationId: locationId ?? _locationId,
            name: "Bystander",
            currentHp: currentHp
        );
        _context.Creatures.Add(bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return bystander;
    }

    private CastAbilityCommand MakeCommand(Creature player, Guid targetId) =>
        new()
        {
            WorldId = _worldId,
            PlayerId = player.Id,
            TargetId = targetId,
            AbilityName = Mend,
            GameTime = TestTime.Start,
        };

    private async Task<Creature> Reload(Guid id) =>
        await _context
            .Creatures.AsNoTracking()
            .SingleAsync(c => c.Id == id, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_HealsTheTarget_AndSpendsTheCasterResources()
    {
        // Arrange
        var player = await SeedPlayer(currentMp: 8);
        var bystander = await SeedBystander(currentHp: 10);

        // Act
        await _handler.Handle(
            MakeCommand(player, bystander.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var healed = await Reload(bystander.Id);
        var caster = await Reload(player.Id);
        Assert.Equal(25, healed.CurrentHp);
        Assert.Equal(6, caster.CurrentMp);
    }

    [Fact]
    public async Task Handle_HealsTheCaster_WhenTheyTargetThemselves()
    {
        // Arrange
        var player = await SeedPlayer();

        // Act
        var result = await _handler.Handle(
            MakeCommand(player, player.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var caster = await Reload(player.Id);
        Assert.Equal(35, caster.CurrentHp);
        Assert.True(result.TargetIsPlayer);
    }

    [Fact]
    public async Task Handle_TrainsTheSkill_OfTheCastAbility()
    {
        // Arrange
        var player = await SeedPlayer();
        var bystander = await SeedBystander();
        var before = await _context
            .CreatureSkills.AsNoTracking()
            .SingleAsync(s => s.CreatureId == player.Id, TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            MakeCommand(player, bystander.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var after = await _context
            .CreatureSkills.AsNoTracking()
            .SingleAsync(s => s.CreatureId == player.Id, TestContext.Current.CancellationToken);
        Assert.True(after.Experience > before.Experience);
    }

    [Fact]
    public async Task Handle_EnqueuesNoCombatEvent()
    {
        // Arrange
        var player = await SeedPlayer();
        var bystander = await SeedBystander();

        // Act
        await _handler.Handle(
            MakeCommand(player, bystander.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        var gameEvents = _serviceProvider.GetRequiredService<TestGameClientEventSink>();
        Assert.Empty(gameEvents.EnqueuedEvents.OfType<CombatUpdatedEvent>());
    }

    [Fact]
    public async Task Handle_Throws_WhenThePlayerIsEngaged()
    {
        // Arrange
        var player = await SeedPlayer(isEngaged: true);
        var bystander = await SeedBystander();

        // Act
        var act = () =>
            _handler.Handle(
                MakeCommand(player, bystander.Id),
                TestContext.Current.CancellationToken
            );

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task Handle_Throws_WhenTheTargetIsElsewhere()
    {
        // Arrange
        var player = await SeedPlayer();
        var faraway = await SeedBystander(locationId: Guid.NewGuid());

        // Act
        var act = () =>
            _handler.Handle(MakeCommand(player, faraway.Id), TestContext.Current.CancellationToken);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
    }

    [Fact]
    public async Task Handle_LeavesTheTargetUntouched_WhenThePlayerCannotAffordTheCast()
    {
        // Arrange
        var player = await SeedPlayer(currentMp: 0);
        var bystander = await SeedBystander(currentHp: 10);

        // Act
        var act = () =>
            _handler.Handle(
                MakeCommand(player, bystander.Id),
                TestContext.Current.CancellationToken
            );

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(act);
        var unchanged = await Reload(bystander.Id);
        Assert.Equal(10, unchanged.CurrentHp);
    }
}
