using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Trading;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Trading;

[Collection("Database")]
public sealed class InventoryItemTransferTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private InventoryItemTransfer _transfer = null!;
    private Creature _source = null!;
    private Creature _destination = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _transfer = _serviceProvider.GetRequiredService<InventoryItemTransfer>();
        _source = Builders.MakeCreature(WorldId);
        _destination = Builders.MakeCreature(WorldId);

        _context.Creatures.AddRange(_source, _destination);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Transfer_Throws_WhenDestinationWouldExceedCarryingCapacity()
    {
        // Arrange
        var item = Builders.MakeWeapon(WorldId, weight: _destination.CarryingCapacity + 1);
        item.Quantity = 1;
        item.Ownership.OwnerId = _source.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _transfer.Transfer(
                new ItemOwnerReference(_source.Id, OwnerType.Creature),
                new ItemOwnerReference(_destination.Id, OwnerType.Creature),
                [new ItemSelection(item.Id, 1)],
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Transfer_Succeeds_WhenWithinDestinationCarryingCapacity()
    {
        // Arrange
        var item = Builders.MakeWeapon(WorldId, weight: _destination.CarryingCapacity);
        item.Quantity = 1;
        item.Ownership.OwnerId = _source.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _transfer.Transfer(
            new ItemOwnerReference(_source.Id, OwnerType.Creature),
            new ItemOwnerReference(_destination.Id, OwnerType.Creature),
            [new ItemSelection(item.Id, 1)],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public async Task Transfer_Succeeds_RegardlessOfCapacity_WhenDestinationIsACorpse()
    {
        // Arrange
        var corpse = Builders.MakeCreature(WorldId, state: CreatureState.Dead);
        _context.Creatures.Add(corpse);
        var item = Builders.MakeWeapon(WorldId, weight: corpse.CarryingCapacity + 1);
        item.Quantity = 1;
        item.Ownership.OwnerId = _source.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var results = await _transfer.Transfer(
            new ItemOwnerReference(_source.Id, OwnerType.Creature),
            new ItemOwnerReference(corpse.Id, OwnerType.Creature),
            [new ItemSelection(item.Id, 1)],
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(results);
    }

    [Fact]
    public async Task Transfer_UsesCurrentItemQuantity_WhenPreflightValidationPrecedesIt()
    {
        // Arrange
        var item = Builders.MakeAmmunition(WorldId);
        item.Quantity = 2;
        item.Ownership.OwnerId = _source.Id;
        item.Ownership.OwnerType = OwnerType.Creature;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();

        var source = new ItemOwnerReference(_source.Id, OwnerType.Creature);
        var selection = new ItemSelection(item.Id, 2);
        await _transfer.Validate(source, [selection], TestContext.Current.CancellationToken);

        await using (var updateContext = db.CreateContext())
        {
            await updateContext
                .Items.Where(candidate => candidate.Id == item.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(candidate => candidate.Quantity, 1),
                    TestContext.Current.CancellationToken
                );
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _transfer.Transfer(
                source,
                new ItemOwnerReference(_destination.Id, OwnerType.Creature),
                [selection],
                TestContext.Current.CancellationToken
            )
        );
    }
}
