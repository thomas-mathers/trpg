using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes;
using TRPG.Application.Crimes.Resolvers;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Resolvers;

[Collection("Database")]
public sealed class KillConsequenceResolverTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private KillConsequenceResolver _resolver = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _resolver = _serviceProvider.GetRequiredService<KillConsequenceResolver>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReportsMovedWitnessAndMarksDeadWitnessDead()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        var movedWitness = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = victim.Id,
            VictimName = victim.Name,
        };
        _context.Creatures.AddRange(victim, movedWitness, deadWitness);
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
    public async Task Handle_ReturnsNoReportedCrimes_WhenAllKillWitnessesAreDead()
    {
        // Arrange
        var victim = Builders.MakeCreature(WorldId, locationId: LocationId);
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = victim.Id,
            VictimName = victim.Name,
        };
        _context.Creatures.AddRange(victim, witness);
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
        Assert.Empty(result);
    }
}
