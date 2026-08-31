using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetLiveHumanoidWitnessesAtLocationQueryTests(DatabaseFixture db)
    : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLiveHumanoidWitnessesAtLocationQueryHandler _handler = null!;
    private readonly Guid _locationId = Guid.NewGuid();

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetLiveHumanoidWitnessesAtLocationQueryHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ExcludesDeadSleepingNonHumanoidAndExcludedCreatures()
    {
        // Arrange
        var witness = Builders.MakeCreature(
            WorldId,
            locationId: _locationId,
            state: CreatureState.Idle
        );
        var dead = Builders.MakeCreature(
            WorldId,
            locationId: _locationId,
            state: CreatureState.Dead
        );
        var sleeping = Builders.MakeCreature(
            WorldId,
            locationId: _locationId,
            state: CreatureState.Sleeping
        );
        var nonHumanoid = Builders.MakeCreature(
            WorldId,
            creatureType: CreatureType.Beast,
            locationId: _locationId,
            state: CreatureState.Idle
        );
        var excluded = Builders.MakeCreature(
            WorldId,
            locationId: _locationId,
            state: CreatureState.Idle
        );
        var elsewhere = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        _context.Creatures.AddRange(witness, dead, sleeping, nonHumanoid, excluded, elsewhere);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = WorldId,
                LocationId = _locationId,
                ExcludeCreatureId = excluded.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var found = Assert.Single(result);
        Assert.Equal(witness.Id, found.Id);
    }
}
