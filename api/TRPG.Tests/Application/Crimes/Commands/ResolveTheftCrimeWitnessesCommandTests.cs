using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Commands;

[Collection("Database")]
public sealed class ResolveTheftCrimeWitnessesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ResolveTheftCrimeWitnessesCommandHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ResolveTheftCrimeWitnessesCommandHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private TheftCrime MakeCrime() =>
        new()
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            OwnerFactionId = Guid.NewGuid(),
            OwnerCreatureId = Guid.NewGuid(),
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
        var result = await _handler.Handle(
            new ResolveTheftCrimeWitnessesCommand
            {
                WorldId = WorldId,
                PlayerId = _player.Id,
                LocationId = LocationId,
                LiveWitnessCreatureIds = [witness.Id],
            },
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
        Assert.Equal(CrimeResolution.Reported, persistedCrime!.Resolution);
        Assert.Equal(CrimeWitnessResolution.Reported, persistedWitness.Resolution);
        var report = Assert.Single(result.ReportedCrimes);
        Assert.Equal(crime.Id, report.TheftCrimeId);
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
        var result = await _handler.Handle(
            new ResolveTheftCrimeWitnessesCommand
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
        Assert.Empty(result.ReportedCrimes);
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
        var result = await _handler.Handle(
            new ResolveTheftCrimeWitnessesCommand
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
        var witnesses = await verifyContext
            .CrimeWitnesses.Where(witness => witness.CrimeId == crime.Id)
            .ToDictionaryAsync(
                witness => witness.CreatureId,
                witness => witness.Resolution,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(CrimeWitnessResolution.Reported, witnesses[movedWitness.Id]);
        Assert.Equal(CrimeWitnessResolution.Dead, witnesses[deadWitness.Id]);
        var report = Assert.Single(result.ReportedCrimes);
        Assert.Equal([movedWitness.Id], report.ReportedWitnessIds);
    }
}
