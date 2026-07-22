using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.WeaponProficiency.Queries;

[Collection("Database")]
public sealed class GetAllWeaponProficienciesQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetAllWeaponProficienciesQueryHandler _handler = null!;
    private Guid _worldId;
    private Guid _creatureId;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetAllWeaponProficienciesQueryHandler(_context);

        _worldId = _creature.WorldId;
        _creatureId = _creature.Id;
        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DefaultsEveryWeaponTypeToZero_WhenCreatureHasNoProficiencies()
    {
        // Act
        var result = await _handler.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(Enum.GetValues<WeaponType>().Length, result.Count);
        Assert.All(Enum.GetValues<WeaponType>(), type => Assert.Equal(0, result[type]));
    }

    [Fact]
    public async Task Handle_ReturnsAllProficienciesForCreature()
    {
        // Arrange
        _context.CreatureWeaponProficiencies.AddRange(
            new CreatureWeaponProficiency
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                WeaponType = WeaponType.Sword,
                Proficiency = 25,
            },
            new CreatureWeaponProficiency
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                WeaponType = WeaponType.Bow,
                Proficiency = 10,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(Enum.GetValues<WeaponType>().Length, result.Count);
        Assert.Equal(25, result[WeaponType.Sword]);
        Assert.Equal(10, result[WeaponType.Bow]);
        Assert.Equal(0, result[WeaponType.Axe]);
    }

    [Fact]
    public async Task Handle_ExcludesProficienciesFromOtherCreatures()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature(_worldId);
        _context.Creatures.Add(otherCreature);
        _context.CreatureWeaponProficiencies.Add(
            new CreatureWeaponProficiency
            {
                WorldId = _worldId,
                CreatureId = otherCreature.Id,
                WeaponType = WeaponType.Sword,
                Proficiency = 99,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result[WeaponType.Sword]);
    }
}
