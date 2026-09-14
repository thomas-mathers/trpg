using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

public sealed class GetLivingHostileCreatureCountAtLocationsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid OtherLocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLivingHostileCreatureCountAtLocationsQueryHandler _handler = null!;
    private readonly EncounterGroup _group = Builders.MakeEncounterGroup(
        WorldId,
        LocationId,
        Guid.NewGuid()
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetLivingHostileCreatureCountAtLocationsQueryHandler>();

        _context.EncounterGroups.Add(_group);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CountsOnlyLivingMembers_AtTheGivenLocations()
    {
        // Arrange
        var living = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        var dead = Builders.MakeCreature(WorldId, state: CreatureState.Dead);
        _context.Creatures.AddRange(living, dead);
        _context.EncounterGroupMembers.AddRange(
            Builders.MakeEncounterGroupMember(WorldId, _group.Id, living.Id),
            Builders.MakeEncounterGroupMember(WorldId, _group.Id, dead.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLivingHostileCreatureCountAtLocationsQuery
            {
                WorldId = WorldId,
                LocationIds = [LocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenTheLocationHasNoEncounterGroup()
    {
        // Act
        var result = await _handler.Handle(
            new GetLivingHostileCreatureCountAtLocationsQuery
            {
                WorldId = WorldId,
                LocationIds = [OtherLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result);
    }
}
