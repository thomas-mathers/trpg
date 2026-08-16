using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Reputations.Commands;

[Collection("Database")]
public sealed class AdjustReputationCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private Guid _creatureId;
    private GetAllReputationsByCreatureIdQueryHandler _getAllByCreatureId = null!;
    private AdjustReputationCommandHandler _handler = null!;
    private readonly Faction _faction = Builders.MakeFaction();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AdjustReputationCommandHandler(_context);
        _getAllByCreatureId = new GetAllReputationsByCreatureIdQueryHandler(_context);

        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _creatureId = await SeedCreatureId();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Guid> SeedCreatureId()
    {
        var creature = Builders.MakeCreature();
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature.Id;
    }

    [Fact]
    public async Task Handle_CreatesReputation_WhenFirstCall()
    {
        // Act
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = _creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 10,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(reputations);
        Assert.Equal(10, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_IncrementsScore_WhenSubsequentCall()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 10,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 5,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Single(reputations);
        Assert.Equal(15, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_SupportsNegativeDelta()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 20,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -8,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(12, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_ClampsScoreToOneHundred_WhenDeltaWouldExceedIt()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 90,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = 90,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(100, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_ClampsScoreToNegativeOneHundred_WhenDeltaWouldExceedIt()
    {
        // Arrange
        var creatureId = await SeedCreatureId();
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -90,
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustReputationCommand
            {
                CreatureId = creatureId,
                TargetId = _faction.Id,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -90,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var reputations = await _getAllByCreatureId.Handle(
            new GetAllReputationsByCreatureIdQuery { CreatureId = creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(-100, reputations.First().Score);
    }

    [Fact]
    public async Task Handle_Throws_WhenFactionTargetDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AdjustReputationCommand
                {
                    CreatureId = _creatureId,
                    TargetId = Guid.NewGuid(),
                    TargetType = ReputationTargetType.Faction,
                    DeltaScore = 10,
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenCreatureTargetDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AdjustReputationCommand
                {
                    CreatureId = _creatureId,
                    TargetId = Guid.NewGuid(),
                    TargetType = ReputationTargetType.Creature,
                    DeltaScore = 10,
                },
                TestContext.Current.CancellationToken
            )
        );
    }
}
