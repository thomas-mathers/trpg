using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class ResolveCrimeConsequencesAtLocationCommandTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveCrimeConsequencesAtLocationCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Faction _ownerFaction = Builders.MakeFaction(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ResolveCrimeConsequencesAtLocationCommandHandler>();

        _context.Creatures.Add(_player);
        _context.Factions.Add(_ownerFaction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReportsLockpickingAndPenalizesReputation_WhenAWitnessIsStillAlive()
    {
        // Arrange
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = SeedBreakInWitnessedBy(witness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ResolveCrimeConsequencesAtLocationCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);

        var reputation = await verifyContext.Reputations.SingleAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _ownerFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(reputation.Score < 0);
    }

    [Fact]
    public async Task Handle_LeavesLockpickingUnreported_WhenEveryWitnessIsDead()
    {
        // Arrange
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = SeedBreakInWitnessedBy(witness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ResolveCrimeConsequencesAtLocationCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Unreported, persistedCrime!.Resolution);

        var hasReputation = await verifyContext.Reputations.AnyAsync(
            r => r.CreatureId == _player.Id && r.TargetId == _ownerFaction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(hasReputation);
    }

    private LockpickingCrime SeedBreakInWitnessedBy(Creature witness)
    {
        var crime = new LockpickingCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            BuildingId = Guid.NewGuid(),
            BuildingName = "Locked Warehouse",
            OwnerFactionId = _ownerFaction.Id,
        };

        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));

        return crime;
    }
}
