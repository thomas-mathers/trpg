using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

public sealed class GetLivingHostileCreatureIdsByLocationQueryTests
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly DatabaseFixture _database;

    // Instance, not static — the class shares one database across tests, and these tests assert on
    // *every* living hostile at a location, so a location id reused across tests would leak rows
    // from one test's arrangement into another's assertion.
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly Guid _otherLocationId = Guid.NewGuid();

    private readonly EncounterGroup _group;
    private readonly EncounterGroup _otherGroup;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLivingHostileCreatureIdsByLocationQueryHandler _handler = null!;

    public GetLivingHostileCreatureIdsByLocationQueryTests(DatabaseFixture database)
    {
        _database = database;
        _group = Builders.MakeEncounterGroup(WorldId, _locationId, Guid.NewGuid());
        _otherGroup = Builders.MakeEncounterGroup(WorldId, _otherLocationId, Guid.NewGuid());
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetLivingHostileCreatureIdsByLocationQueryHandler>();

        _context.EncounterGroups.AddRange(_group, _otherGroup);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyLivingMembers_PerRequestedLocation()
    {
        // Arrange
        var living = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        var dead = Builders.MakeCreature(WorldId, state: CreatureState.Dead);
        var otherLiving = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        _context.Creatures.AddRange(living, dead, otherLiving);
        _context.EncounterGroupMembers.AddRange(
            Builders.MakeEncounterGroupMember(WorldId, _group.Id, living.Id),
            Builders.MakeEncounterGroupMember(WorldId, _group.Id, dead.Id),
            Builders.MakeEncounterGroupMember(WorldId, _otherGroup.Id, otherLiving.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = WorldId,
                LocationIds = [_locationId, _otherLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal([living.Id], result[_locationId]);
        Assert.Equal([otherLiving.Id], result[_otherLocationId]);
    }

    [Fact]
    public async Task Handle_OmitsTheLocation_WhenItHasNoLivingHostiles()
    {
        // Act
        var result = await _handler.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = WorldId,
                LocationIds = [_locationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
