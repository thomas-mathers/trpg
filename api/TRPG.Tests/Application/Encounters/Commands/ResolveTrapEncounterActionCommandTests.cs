using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class ResolveTrapEncounterActionCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly TestChanceRoller _chanceRoller = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveTrapEncounterActionCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Location _targetLocation = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private GameSession _session = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveTrapEncounterActionCommandHandler>();

        _player.LocationId = _location.Id;
        _session = Builders.MakeGameSession(WorldId, _player.Id);
        _context.Locations.AddRange(_location, _targetLocation);
        _context.Creatures.Add(_player);
        _context.GameSessions.Add(_session);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<(Trigger Trigger, TrapEncounter Encounter)> SeedTrapEncounter(TrapKind kind)
    {
        var trigger = Builders.MakeTrigger(
            WorldId,
            locationId: _location.Id,
            targetId: _targetLocation.Id,
            trapKind: kind
        );
        var encounter = new TrapEncounter
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = _location.Id,
            LocationName = "Test Room",
            TriggerId = trigger.Id,
            TrapKind = kind,
            TargetLocationId = _targetLocation.Id,
            TargetLocationName = "Target Room",
        };
        _context.Props.Add(trigger);
        _context.Encounters.Add(encounter);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (trigger, encounter);
    }

    private ResolveTrapEncounterActionCommand MakeCommand(
        TrapEncounterAction action,
        Guid encounterId
    ) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            SessionId = _session.Id,
            Action = action,
            EncounterId = encounterId,
        };

    [Fact]
    public async Task Handle_Withdraw_LeavesTheTrapUnresolvedAndDoesNotRelocate()
    {
        // Arrange
        var (trigger, encounter) = await SeedTrapEncounter(TrapKind.Water);

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new WithdrawTrapAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TrapEncounterResolutionOutcome.Withdrew, fact.Outcome);

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_location.Id, player!.LocationId);
        var resolvedTrigger = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.False(resolvedTrigger.IsResolved);
    }

    [Fact]
    public async Task Handle_Disarm_ResolvesTheTrapWithoutRelocating_WhenTheRollSucceeds()
    {
        // Arrange
        var (trigger, encounter) = await SeedTrapEncounter(TrapKind.Mechanical);
        _chanceRoller.Result = true;

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new DisarmTrapAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TrapEncounterResolutionOutcome.Disarmed, fact.Outcome);

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_location.Id, player!.LocationId);
        var resolvedTrigger = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.True(resolvedTrigger.IsResolved);
    }

    [Fact]
    public async Task Handle_Disarm_FallsAndRelocates_WhenTheRollFails()
    {
        // Arrange
        var (_, encounter) = await SeedTrapEncounter(TrapKind.Mechanical);
        _chanceRoller.Result = false;

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new DisarmTrapAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TrapEncounterResolutionOutcome.Fell, fact.Outcome);

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_targetLocation.Id, player!.LocationId);
    }

    [Fact]
    public async Task Handle_Disarm_Throws_WhenTheTrapIsNotMechanical()
    {
        // Arrange
        var (_, encounter) = await SeedTrapEncounter(TrapKind.Slope);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                MakeCommand(new DisarmTrapAction(), encounter.Id),
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Attempt_ResolvesTheTrapWithoutRelocating_WhenTheRollSucceeds()
    {
        // Arrange
        var (trigger, encounter) = await SeedTrapEncounter(TrapKind.Slope);
        _chanceRoller.Result = true;

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new AttemptTrapAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TrapEncounterResolutionOutcome.Survived, fact.Outcome);

        await using var verifyContext = db.CreateContext();
        var resolvedTrigger = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.True(resolvedTrigger.IsResolved);
    }

    [Fact]
    public async Task Handle_Attempt_FallsAndRelocates_WhenTheRollFails()
    {
        // Arrange
        var (_, encounter) = await SeedTrapEncounter(TrapKind.Water);
        _chanceRoller.Result = false;

        // Act
        var fact = await _handler.Handle(
            MakeCommand(new AttemptTrapAction(), encounter.Id),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(TrapEncounterResolutionOutcome.Fell, fact.Outcome);
        Assert.Equal("Target Room", fact.TargetLocationName);

        await using var verifyContext = db.CreateContext();
        var player = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_targetLocation.Id, player!.LocationId);
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; } = true;

        public bool Roll(float chance) => Result;
    }
}
