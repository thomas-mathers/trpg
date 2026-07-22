using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetUnallocatedAttributePointsQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetUnallocatedAttributePointsQueryHandler _handler = null!;
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
        _handler = new GetUnallocatedAttributePointsQueryHandler(
            _context,
            Builders.MakeStatFormulas(new CreatureGeneratorOptions { PointsPerLevel = 5 })
        );

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsExpectedMinusCurrentTotal()
    {
        // Arrange — 7 base stats at 1 each = 7; expected = 7 + level(1) * pointsPerLevel(5) = 12
        // Act
        var unallocated = await _handler.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(5, unallocated);
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenFullyAllocated()
    {
        // Arrange — spend the 5 available points
        _creature.BaseAttributes.Strength += 5;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var unallocated = await _handler.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, unallocated);
    }

    [Fact]
    public async Task Handle_GrowsWithCharacterLevel()
    {
        // Arrange — leveling up from 1 to 3 should grant 2 * pointsPerLevel(5) = 10 more points
        _creature.Level = 3;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var unallocated = await _handler.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(15, unallocated);
    }
}
