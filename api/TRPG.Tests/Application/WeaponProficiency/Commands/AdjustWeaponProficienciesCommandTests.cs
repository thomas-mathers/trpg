using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WeaponProficiency.Commands;

[Collection("Database")]
public sealed class AdjustWeaponProficienciesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private AdjustWeaponProficienciesCommandHandler _handler = null!;
    private GetAllWeaponProficienciesQueryHandler _getAllWeaponProficiencies = null!;
    private Guid _worldId;
    private Guid _creatureId;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _getAllWeaponProficiencies = new GetAllWeaponProficienciesQueryHandler(_context);
        _handler = new AdjustWeaponProficienciesCommandHandler(_context);

        _worldId = _creature.WorldId;
        _creatureId = _creature.Id;
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_CreatesProficiency_WhenNoneExists()
    {
        // Act
        await _handler.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                ProficiencyDeltas = new Dictionary<WeaponType, int> { [WeaponType.Sword] = 5 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var proficiencies = await _getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(5, proficiencies[WeaponType.Sword]);
    }

    [Fact]
    public async Task Handle_AddsToExistingProficiency()
    {
        // Arrange
        await _handler.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                ProficiencyDeltas = new Dictionary<WeaponType, int> { [WeaponType.Sword] = 5 },
            },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                ProficiencyDeltas = new Dictionary<WeaponType, int> { [WeaponType.Sword] = 3 },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var proficiencies = await _getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(8, proficiencies[WeaponType.Sword]);
    }

    [Fact]
    public async Task Handle_AdjustsMultipleWeaponTypesInOneCall()
    {
        // Act
        await _handler.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                ProficiencyDeltas = new Dictionary<WeaponType, int>
                {
                    [WeaponType.Sword] = 5,
                    [WeaponType.Bow] = 2,
                },
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var proficiencies = await _getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.Equal(5, proficiencies[WeaponType.Sword]);
        Assert.Equal(2, proficiencies[WeaponType.Bow]);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenProficiencyDeltasIsEmpty()
    {
        // Act
        await _handler.Handle(
            new AdjustWeaponProficienciesCommand
            {
                WorldId = _worldId,
                CreatureId = _creatureId,
                ProficiencyDeltas = new Dictionary<WeaponType, int>(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var proficiencies = await _getAllWeaponProficiencies.Handle(
            new GetAllWeaponProficienciesQuery { WorldId = _worldId, CreatureId = _creatureId },
            TestContext.Current.CancellationToken
        );
        Assert.All(proficiencies.Values, proficiency => Assert.Equal(0, proficiency));
    }
}
