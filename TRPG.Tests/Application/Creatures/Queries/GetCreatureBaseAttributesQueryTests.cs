using TRPG.Application.Creatures.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.Creatures.Queries;

[Collection("Database")]
public sealed class GetCreatureBaseAttributesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetCreatureBaseAttributesQueryHandler _handler = null!;
    private readonly Creature _creature = MakeSeedCreature();

    private static Creature MakeSeedCreature()
    {
        var creature = Builders.MakeCreature();
        creature.BaseAttributes = new Attributes
        {
            Strength = 3,
            Defense = 4,
            Dexterity = 5,
            Endurance = 6,
            Stamina = 7,
            Mana = 8,
            Intelligence = 9,
        };
        return creature;
    }

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetCreatureBaseAttributesQueryHandler(_context);

        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsCreatureBaseAttributes()
    {
        // Act
        var attributes = await _handler.Handle(
            new GetCreatureBaseAttributesQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(3, attributes.Strength);
        Assert.Equal(4, attributes.Defense);
        Assert.Equal(5, attributes.Dexterity);
        Assert.Equal(6, attributes.Endurance);
        Assert.Equal(7, attributes.Stamina);
        Assert.Equal(8, attributes.Mana);
        Assert.Equal(9, attributes.Intelligence);
    }
}
