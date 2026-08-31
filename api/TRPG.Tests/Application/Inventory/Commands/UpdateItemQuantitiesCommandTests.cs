using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class UpdateItemQuantitiesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private UpdateItemQuantitiesCommandHandler _handler = null!;
    private readonly Item _first = Builders.MakeItem(WorldId);
    private readonly Item _second = Builders.MakeItem(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<UpdateItemQuantitiesCommandHandler>();

        _context.Items.AddRange(_first, _second);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesEachItemToItsOwnQuantity()
    {
        // Act
        await _handler.Handle(
            new UpdateItemQuantitiesCommand
            {
                Updates =
                [
                    new ItemQuantityUpdate(_first.Id, 5),
                    new ItemQuantityUpdate(_second.Id, 9),
                ],
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var first = await verifyContext.Items.SingleAsync(
            item => item.Id == _first.Id,
            TestContext.Current.CancellationToken
        );
        var second = await verifyContext.Items.SingleAsync(
            item => item.Id == _second.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(5, first.Quantity);
        Assert.Equal(9, second.Quantity);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoUpdatesGiven()
    {
        // Act & Assert — no exception, no-op
        await _handler.Handle(
            new UpdateItemQuantitiesCommand { Updates = [] },
            TestContext.Current.CancellationToken
        );
    }
}
