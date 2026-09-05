using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class EvaluateSuspicionEncounterCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly TestChanceRoller _chanceRoller = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private EvaluateSuspicionEncounterCommandHandler _handler = null!;
    private readonly Location _location = Builders.MakeLocation(WorldId, Guid.NewGuid());
    private readonly Creature _player = Builders.MakeCreature(WorldId, isSneaking: true);
    private readonly Faction _cityFaction = Builders.MakeFaction(WorldId, isCityFaction: true);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .Configure<SuspicionOptions>(new ConfigurationBuilder().Build())
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<EvaluateSuspicionEncounterCommandHandler>();

        _player.LocationId = _location.Id;
        _context.Locations.Add(_location);
        _context.Creatures.Add(_player);
        _context.Factions.Add(_cityFaction);
        _context.GameSessions.Add(Builders.MakeGameSession(WorldId, _player.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedGuard()
    {
        var guard = Builders.MakeCreature(
            WorldId,
            profession: Profession.Guard,
            locationId: _location.Id
        );
        _context.Creatures.Add(guard);
        _context.FactionMembers.Add(Builders.MakeFactionMember(WorldId, _cityFaction.Id, guard.Id));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return guard;
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenThePlayerIsNotSneaking()
    {
        // Arrange
        await SeedGuard();
        _player.IsSneaking = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new EvaluateSuspicionEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoGuardIsAtTheLocation()
    {
        // Arrange
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new EvaluateSuspicionEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNullAndLeavesTheStanceIntact_WhenTheDetectionRollFails()
    {
        // Arrange
        await SeedGuard();
        _chanceRoller.Result = false;

        // Act
        var result = await _handler.Handle(
            new EvaluateSuspicionEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
        await using var verifyContext = db.CreateContext();
        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedPlayer!.IsSneaking);
    }

    [Fact]
    public async Task Handle_CreatesEncounterAndClearsTheStance_WhenTheDetectionRollSucceeds()
    {
        // Arrange
        var guard = await SeedGuard();
        _chanceRoller.Result = true;

        // Act
        var result = await _handler.Handle(
            new EvaluateSuspicionEncounterCommand { WorldId = WorldId, PlayerId = _player.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(guard.Id, result.GuardCreatureId);
        Assert.Equal(_cityFaction.Id, result.CityFactionId);
        Assert.Equal(SuspicionCause.Sneaking, result.Cause);

        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext
            .Encounters.OfType<SuspicionEncounter>()
            .SingleAsync(e => e.PlayerId == _player.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EncounterState.Active, persisted.State);

        var updatedPlayer = await verifyContext.Creatures.FindAsync(
            [_player.Id],
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedPlayer!.IsSneaking);
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; } = true;

        public bool Roll(float chance) => Result;
    }
}
