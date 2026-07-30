using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    private ServiceProvider _serviceProvider = null!;
    private GetUnallocatedAttributePointsQueryHandler _handler = null!;

    // Matches CreatureGeneratorOptions' default BaseAttributes (5 each, Total 35) so the
    // creature starts with exactly options.PointsPerLevel(5) unallocated at level 1.
    private readonly Creature _creature = Builders.MakeCreature(
        level: 1,
        baseAttributes: new Attributes
        {
            Strength = 5,
            Defense = 5,
            Dexterity = 5,
            Endurance = 5,
            Stamina = 5,
            Mana = 5,
            Intelligence = 5,
        }
    );

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CreatureGeneratorOptions>>(
                new TestOptionsSnapshot<CreatureGeneratorOptions>(
                    new CreatureGeneratorOptions { PointsPerLevel = 5 }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetUnallocatedAttributePointsQueryHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsExpectedMinusCurrentTotal()
    {
        // Arrange — 7 base stats at 5 each = 35; expected = 35 + level(1) * pointsPerLevel(5) = 40
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
