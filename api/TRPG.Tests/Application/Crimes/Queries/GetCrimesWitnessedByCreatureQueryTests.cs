using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Crimes.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Crimes.Queries;

[Collection("Database")]
public sealed class GetCrimesWitnessedByCreatureQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetCrimesWitnessedByCreatureQueryHandler _handler = null!;
    private readonly Creature _player = Builders.MakeCreature(WorldId, locationId: LocationId);
    private readonly Creature _witness = Builders.MakeCreature(WorldId, locationId: LocationId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetCrimesWitnessedByCreatureQueryHandler>();

        _context.Creatures.AddRange(_player, _witness);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEveryCrimeKindTheWitnessSaw_OrderedByMostRecentFirst()
    {
        // Arrange
        var kill = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = Guid.NewGuid(),
            VictimName = "Victim",
            OccurredAt = DateTime.UtcNow.AddMinutes(-30),
        };
        var theft = new TheftCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            OwnerCreatureId = Guid.NewGuid(),
            OwnerName = "Mara",
            Outcome = TheftCrimeOutcome.Taken,
            SourceOwnerId = Guid.NewGuid(),
            SourceOwnerType = OwnerType.Container,
            OccurredAt = DateTime.UtcNow.AddMinutes(-20),
        };
        var breakIn = new BreakingAndEnteringCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            BuildingId = Guid.NewGuid(),
            BuildingName = "The Sundry Store",
            OccurredAt = DateTime.UtcNow.AddMinutes(-10),
        };
        _context.Crimes.AddRange(kill, theft, breakIn);
        _context.CrimeWitnesses.AddRange(
            Builders.MakeCrimeWitness(kill.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(theft.Id, _witness.Id, WorldId),
            Builders.MakeCrimeWitness(breakIn.Id, _witness.Id, WorldId)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCrimesWitnessedByCreatureQuery
            {
                WorldId = WorldId,
                WitnessCreatureId = _witness.Id,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — OccurredAt round-trips through Postgres with lower precision than the in-memory
        // DateTime, so compare it separately from the fields that survive the round trip exactly.
        Assert.Equal(
            [
                (
                    WitnessedCrimeKind.BreakingAndEntering,
                    "The Sundry Store",
                    (TheftCrimeOutcome?)null
                ),
                (WitnessedCrimeKind.Theft, "Mara", TheftCrimeOutcome.Taken),
                (WitnessedCrimeKind.Kill, "Victim", null),
            ],
            result.Select(crime => (crime.Kind, crime.SubjectName, crime.Outcome))
        );
        Assert.True(result[0].OccurredAt > result[1].OccurredAt);
        Assert.True(result[1].OccurredAt > result[2].OccurredAt);
    }

    [Fact]
    public async Task Handle_ExcludesACrime_WhenTheWitnessEndedUpDead()
    {
        // Arrange
        var kill = new KillCrime
        {
            WorldId = WorldId,
            PlayerId = _player.Id,
            LocationId = LocationId,
            VictimId = Guid.NewGuid(),
            VictimName = "Victim",
        };
        _context.Crimes.Add(kill);
        _context.CrimeWitnesses.Add(
            Builders.MakeCrimeWitness(
                kill.Id,
                _witness.Id,
                WorldId,
                resolution: CrimeWitnessResolution.Dead
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCrimesWitnessedByCreatureQuery
            {
                WorldId = WorldId,
                WitnessCreatureId = _witness.Id,
                PlayerId = _player.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
