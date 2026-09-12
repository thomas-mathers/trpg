using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes;
using TRPG.Application.Crimes.Resolvers;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Resolvers;

public sealed class TheftConsequenceResolverTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private TheftConsequenceResolver _resolver = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _resolver = _serviceProvider.GetRequiredService<TheftConsequenceResolver>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private TheftCrime MakeCrime(Guid? ownerCreatureId = null) =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            OwnerFactionId = Guid.NewGuid(),
            OwnerCreatureId = ownerCreatureId ?? Guid.NewGuid(),
            OwnerName = "Mara",
            Outcome = TheftCrimeOutcome.Taken,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
        };

    [Fact]
    public async Task Handle_MarksCrimeAndWitnessReported_WhenWitnessSurvivesAtTheScene()
    {
        // Arrange
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeCrime();
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [witness.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id && item.CreatureId == witness.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Reported, persistedWitness.Resolution);
        Assert.Equal(CrimeWitnessKind.Saw, persistedWitness.Kind);
        var report = Assert.Single(result);
        Assert.Equal([witness.Id], report.ReportedWitnessIds);
    }

    [Fact]
    public async Task Handle_ReturnsNoReportedCrimes_WhenAllWitnessesAreDead()
    {
        // Arrange
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = MakeCrime();
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var persistedCrime = await verifyContext.Crimes.FindAsync(
            [crime.Id],
            TestContext.Current.CancellationToken
        );
        var persistedWitness = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Unreported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Dead, persistedWitness.Resolution);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReportsLivingWitnessesElsewhere()
    {
        // Arrange
        var movedWitness = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = MakeCrime();
        _context.Creatures.AddRange(movedWitness, deadWitness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.AddRange(
            Builders.MakeCrimeWitness(crime.Id, movedWitness.Id, WorldId),
            Builders.MakeCrimeWitness(crime.Id, deadWitness.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [movedWitness.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var witnesses = await verifyContext
            .CrimeWitnesses.Where(witness => witness.CrimeId == crime.Id)
            .ToDictionaryAsync(
                witness => witness.CreatureId,
                witness => witness.Resolution,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(CrimeWitnessResolution.Reported, witnesses[movedWitness.Id]);
        Assert.Equal(CrimeWitnessResolution.Dead, witnesses[deadWitness.Id]);
        var report = Assert.Single(result);
        Assert.Equal([movedWitness.Id], report.ReportedWitnessIds);
    }

    [Fact]
    public async Task Handle_TellsAnAbsentVictimWhoRobbedThem_WhenTheCrimeIsReported()
    {
        // Arrange
        var witness = Builders.MakeCreature(WorldId, locationId: LocationId);
        var absentOwner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var crime = MakeCrime(absentOwner.Id);
        _context.Creatures.AddRange(witness, absentOwner);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [witness.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var hearsay = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id && item.CreatureId == absentOwner.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeWitnessKind.Heard, hearsay.Kind);
        Assert.Equal(CrimeWitnessResolution.Reported, hearsay.Resolution);
    }

    [Fact]
    public async Task Handle_TellsTheVictimNothing_WhenEveryWitnessIsDead()
    {
        // Arrange — silencing everyone who saw it leaves nobody to carry word to the owner
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var absentOwner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var crime = MakeCrime(absentOwner.Id);
        _context.Creatures.AddRange(deadWitness, absentOwner);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, deadWitness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var toldTheOwner = await verifyContext.CrimeWitnesses.AnyAsync(
            item => item.CrimeId == crime.Id && item.CreatureId == absentOwner.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(toldTheOwner);
    }

    [Fact]
    public async Task Handle_LeavesAPresentVictimAsHavingSeenIt_RatherThanHavingHeardOfIt()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: LocationId);
        var crime = MakeCrime(owner.Id);
        _context.Creatures.Add(owner);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, owner.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _resolver.Resolve(
            new CrimeScope(WorldId, _player.Id, LocationId),
            [owner.Id],
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var row = await verifyContext.CrimeWitnesses.SingleAsync(
            item => item.CrimeId == crime.Id && item.CreatureId == owner.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeWitnessKind.Saw, row.Kind);
    }
}
