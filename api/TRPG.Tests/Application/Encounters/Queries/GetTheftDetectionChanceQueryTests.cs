using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Inventory;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Queries;

[Collection("Database")]
public sealed class GetTheftDetectionChanceQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _theftLocationId = Guid.NewGuid();
    private readonly Creature _player = Builders.MakeCreature(WorldId);
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetTheftDetectionChanceQueryHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<TheftOptions>>(
                new TestOptionsMonitor<TheftOptions>(
                    new TheftOptions
                    {
                        BaseDetectionChance = 0.5f,
                        MaximumDetectionChance = 0.95f,
                        MinimumDetectionChance = 0.05f,
                        DetectionChanceReductionPerSkillLevel = 0.05f,
                        DetectionChanceIncreasePerItem = 0.05f,
                        DetectionChanceIncreasePerEquippedItem = 0.2f,
                    }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetTheftDetectionChanceQueryHandler>();

        _context.Creatures.Add(_player);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoItemsAreSelected()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        _context.Creatures.Add(owner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                From = new ItemOwnerReference(owner.Id, OwnerType.Creature),
                Items = [],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheContainerHasNoOwner()
    {
        // Arrange
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                From = new ItemOwnerReference(container.Id, OwnerType.Container),
                Items = [new ItemSelection(Guid.NewGuid(), 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsGuaranteedSuccess_WhenNoOneCanWitnessTheTheft()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        container.OwnerCreatureId = owner.Id;
        _context.Creatures.Add(owner);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                From = new ItemOwnerReference(container.Id, OwnerType.Container),
                Items = [new ItemSelection(Guid.NewGuid(), 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(1f, result);
    }

    [Fact]
    public async Task Handle_ScalesDownWithQuantityAndUpWithSkill_WhenPickpocketing()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        _context.Creatures.Add(owner);
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.Pickpocketing,
                Level = 10,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                From = new ItemOwnerReference(owner.Id, OwnerType.Creature),
                Items = [new ItemSelection(Guid.NewGuid(), 5)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert — detectionChance = 0.5 + 5*0.05 - 10*0.05 = 0.25, success = 0.75
        Assert.Equal(0.75f, result);
    }

    [Fact]
    public async Task Handle_ScalesDownFurther_WhenTheSelectedItemIsEquipped()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var item = Builders.MakeWeapon(WorldId, quantity: 1);
        item.Ownership.OwnerId = owner.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        item.Ownership.EquippedSlot = EquipmentSlot.RightHand;
        _context.Creatures.Add(owner);
        _context.Items.Add(item);
        _context.CreatureSkills.Add(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.Pickpocketing,
                Level = 10,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = _player.Id,
                WorldId = WorldId,
                From = new ItemOwnerReference(owner.Id, OwnerType.Creature),
                Items = [new ItemSelection(item.Id, 1)],
            },
            TestContext.Current.CancellationToken
        );

        // Assert — detectionChance = 0.5 + 1*0.05 - 10*0.05 + 1*0.2 = 0.25, success = 0.75
        Assert.Equal(0.75f, result);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
