using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

public sealed class GetLivingHostileCreatureCountsByLocationQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly Guid LocationId = Guid.NewGuid();
    private static readonly Guid OtherLocationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetLivingHostileCreatureCountsByLocationQueryHandler _handler = null!;
    private readonly EncounterGroup _group = Builders.MakeEncounterGroup(
        WorldId,
        LocationId,
        Guid.NewGuid()
    );
    private readonly EncounterGroup _otherGroup = Builders.MakeEncounterGroup(
        WorldId,
        OtherLocationId,
        Guid.NewGuid()
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<GetLivingHostileCreatureCountsByLocationQueryHandler>();

        _context.EncounterGroups.AddRange(_group, _otherGroup);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CountsOnlyLivingMembers_PerRequestedLocation()
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
            new GetLivingHostileCreatureCountsByLocationQuery
            {
                WorldId = WorldId,
                LocationIds = [LocationId, OtherLocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1, result[LocationId]);
        Assert.Equal(1, result[OtherLocationId]);
    }

    [Fact]
    public async Task Handle_OmitsTheLocation_WhenItHasNoLivingHostiles()
    {
        // Act
        var result = await _handler.Handle(
            new GetLivingHostileCreatureCountsByLocationQuery
            {
                WorldId = WorldId,
                LocationIds = [LocationId],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
