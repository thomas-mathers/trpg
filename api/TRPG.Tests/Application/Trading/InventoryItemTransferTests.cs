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
