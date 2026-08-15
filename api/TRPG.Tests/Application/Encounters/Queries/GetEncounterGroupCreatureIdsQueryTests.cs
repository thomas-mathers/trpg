using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Encounters.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

[Collection("Database")]
public sealed class GetEncounterGroupCreatureIdsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetEncounterGroupCreatureIdsQueryHandler _handler = null!;
    private readonly EncounterGroup _group = Builders.MakeEncounterGroup(
        WorldId,
        Guid.NewGuid(),
        Guid.NewGuid()
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetEncounterGroupCreatureIdsQueryHandler>();

        _context.EncounterGroups.Add(_group);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheGivenCreature_WhenNotAGroupMember()
    {
        // Arrange
        var loneCreatureId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(
            new GetEncounterGroupCreatureIdsQuery
            {
                WorldId = WorldId,
                CreatureId = loneCreatureId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(loneCreatureId, Assert.Single(result));
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheAttackedCreature_WhenEntireGroupIsAsleep()
    {
        // Arrange
        var target = Builders.MakeCreature(WorldId, state: CreatureState.Sleeping);
        var packmate = Builders.MakeCreature(WorldId, state: CreatureState.Sleeping);
        await SeedGroupMembers(target, packmate);

        // Act
        var result = await _handler.Handle(
            new GetEncounterGroupCreatureIdsQuery { WorldId = WorldId, CreatureId = target.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(target.Id, Assert.Single(result));
    }

    [Fact]
    public async Task Handle_ReturnsAllLivingMembers_WhenAtLeastOneMemberIsAwake()
    {
        // Arrange
        var target = Builders.MakeCreature(WorldId, state: CreatureState.Sleeping);
        var awakePackmate = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        await SeedGroupMembers(target, awakePackmate);

        // Act
        var result = await _handler.Handle(
            new GetEncounterGroupCreatureIdsQuery { WorldId = WorldId, CreatureId = target.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(
            new[] { target.Id, awakePackmate.Id }.OrderBy(id => id),
            result.OrderBy(id => id)
        );
    }

    [Fact]
    public async Task Handle_ExcludesDeadMembers_WhenAtLeastOneMemberIsAwake()
    {
        // Arrange
        var target = Builders.MakeCreature(WorldId, state: CreatureState.Idle);
        var deadPackmate = Builders.MakeCreature(WorldId, state: CreatureState.Dead);
        await SeedGroupMembers(target, deadPackmate);

        // Act
        var result = await _handler.Handle(
            new GetEncounterGroupCreatureIdsQuery { WorldId = WorldId, CreatureId = target.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(target.Id, Assert.Single(result));
    }

    private async Task SeedGroupMembers(params Creature[] members)
    {
        _context.Creatures.AddRange(members);
        _context.EncounterGroupMembers.AddRange(
            members.Select(member =>
                Builders.MakeEncounterGroupMember(WorldId, _group.Id, member.Id)
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
