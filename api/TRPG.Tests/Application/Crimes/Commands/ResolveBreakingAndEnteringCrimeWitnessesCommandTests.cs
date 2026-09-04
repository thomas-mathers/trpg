using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class ResolveBreakingAndEnteringCrimeWitnessesCommandTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveBreakingAndEnteringCrimeWitnessesCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<ResolveBreakingAndEnteringCrimeWitnessesCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReportsCrime_WhenAtLeastOneWitnessIsStillAlive()
    {
        // Arrange
        var movedWitness = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var deadWitness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = new BreakingAndEnteringCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            BuildingId = Guid.NewGuid(),
            BuildingName = "Test Building",
        };
        _context.Creatures.AddRange(movedWitness, deadWitness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.AddRange(
            Builders.MakeCrimeWitness(crime.Id, movedWitness.Id, WorldId),
            Builders.MakeCrimeWitness(crime.Id, deadWitness.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveBreakingAndEnteringCrimeWitnessesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
                LiveWitnessCreatureIds = [movedWitness.Id],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var storedCrime = await verifyContext.Crimes.FirstAsync(
            c => c.Id == crime.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Reported, storedCrime.Resolution);
        var report = Assert.Single(result.ReportedCrimes);
        Assert.Equal(crime.Id, report.CrimeId);
        Assert.Equal([movedWitness.Id], report.ReportedWitnessIds);
    }

    [Fact]
    public async Task Handle_ReturnsNoReportedCrimes_WhenTheLastRemainingWitnessIsDead()
    {
        // Arrange
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: LocationId,
            state: CreatureState.Dead
        );
        var crime = new BreakingAndEnteringCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            BuildingId = Guid.NewGuid(),
            BuildingName = "Test Building",
        };
        _context.Creatures.Add(witness);
        _context.Crimes.Add(crime);
        _context.CrimeWitnesses.Add(Builders.MakeCrimeWitness(crime.Id, witness.Id, WorldId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ResolveBreakingAndEnteringCrimeWitnessesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
                LiveWitnessCreatureIds = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var storedCrime = await verifyContext.Crimes.FirstAsync(
            c => c.Id == crime.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CrimeResolution.Unreported, storedCrime.Resolution);
        Assert.Empty(result.ReportedCrimes);
    }
}
