using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class AddItemsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddItemsCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddItemsCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsAllItems()
    {
        // Arrange
        var first = Builders.MakeItem(WorldId);
        var second = Builders.MakeItem(WorldId);

        // Act
        await _handler.Handle(
            new AddItemsCommand { Items = [first, second] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.Items.AnyAsync(
                item => item.Id == first.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Items.AnyAsync(
                item => item.Id == second.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
