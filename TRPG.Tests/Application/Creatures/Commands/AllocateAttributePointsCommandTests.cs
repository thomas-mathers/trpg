using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AllocateAttributePointsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private AllocateAttributePointsCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(
        level: 1,
        baseAttributes: new Attributes
        {
            Strength = 1,
            Defense = 1,
            Dexterity = 1,
            Endurance = 1,
            Stamina = 1,
            Mana = 1,
            Intelligence = 1,
        }
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AllocateAttributePointsCommandHandler(
            _context,
            new GetInventoryByCreatureIdQueryHandler(_context),
            Builders.MakeStatFormulas(new CreatureGeneratorOptions { PointsPerLevel = 5 })
        );

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    private async Task<Creature> ReloadCreature()
    {
        await using var freshContext = db.CreateContext();
        return (
            await freshContext.Creatures.FindAsync(
                [_creature.Id],
                TestContext.Current.CancellationToken
            )
        )!;
    }

    [Fact]
    public async Task Handle_AppliesDeltas_ToBaseAttributes()
    {
        // Arrange — 5 points available (7 + 1*5 - 7)
        // Act
        await _handler.Handle(
            new AllocateAttributePointsCommand
            {
                CreatureId = _creature.Id,
                Deltas = new Dictionary<AllocatableAttributeName, int>
                {
                    [AllocatableAttributeName.Strength] = 3,
                    [AllocatableAttributeName.Endurance] = 2,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(4, creature.BaseAttributes.Strength);
        Assert.Equal(3, creature.BaseAttributes.Endurance);
    }

    [Fact]
    public async Task Handle_RecalculatesMaximumHp_FromUpdatedEndurance()
    {
        // Act — endurance +2 should raise MaximumHp (5 hp per endurance point)
        await _handler.Handle(
            new AllocateAttributePointsCommand
            {
                CreatureId = _creature.Id,
                Deltas = new Dictionary<AllocatableAttributeName, int>
                {
                    [AllocatableAttributeName.Endurance] = 2,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(15, creature.BaseAttributes.MaximumHp);
        Assert.Equal(15, creature.MaximumHp);
    }

    [Fact]
    public async Task Handle_Throws_WhenRequestedTotalExceedsUnallocatedPoints()
    {
        // Act & Assert — only 5 points available, requesting 6
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AllocateAttributePointsCommand
                {
                    CreatureId = _creature.Id,
                    Deltas = new Dictionary<AllocatableAttributeName, int>
                    {
                        [AllocatableAttributeName.Strength] = 6,
                    },
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenADeltaIsNegative()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(
                new AllocateAttributePointsCommand
                {
                    CreatureId = _creature.Id,
                    Deltas = new Dictionary<AllocatableAttributeName, int>
                    {
                        [AllocatableAttributeName.Strength] = -1,
                    },
                },
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenDeltasIsEmpty()
    {
        // Act
        await _handler.Handle(
            new AllocateAttributePointsCommand
            {
                CreatureId = _creature.Id,
                Deltas = new Dictionary<AllocatableAttributeName, int>(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var creature = await ReloadCreature();
        Assert.Equal(1, creature.BaseAttributes.Strength);
    }
}
