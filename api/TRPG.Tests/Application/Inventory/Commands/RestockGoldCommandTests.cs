using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Commands;

[Collection("Database")]
public sealed class RestockGoldCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    // Per test: the suite shares one database, and only one gold row may exist per owner.
    private readonly Guid _workstationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RestockGoldCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<RestockGoldCommandHandler>();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_TopsUpAnEmptiedRow_RatherThanAddingASecondOne()
    {
        // Arrange — a workstation traded down to nothing still owns its gold row, and the
        // one-row-per-owner constraint rejects any attempt to create another.
        SeedGold(quantity: 0);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == _workstationId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(500, gold.Quantity);
    }

    [Fact]
    public async Task Handle_CreatesTheRow_WhenTheOwnerHasNoGoldAtAll()
    {
        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == _workstationId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(500, gold.Quantity);
    }

    [Fact]
    public async Task Handle_LeavesAWellStockedOwnerAlone()
    {
        // Arrange
        SeedGold(quantity: 900);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(MakeCommand(), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var gold = await verifyContext
            .Items.OfType<Gold>()
            .SingleAsync(
                item => item.Ownership.OwnerId == _workstationId,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(900, gold.Quantity);
    }

    private RestockGoldCommand MakeCommand() =>
        new()
        {
            Owner = new ItemOwnerReference(_workstationId, OwnerType.Workstation),
            WorldId = WorldId,
            MinimumQuantity = 500,
        };

    private void SeedGold(int quantity) =>
        _context.Items.Add(
            new Gold
            {
                WorldId = WorldId,
                Name = "Gold",
                Quantity = quantity,
                Ownership = new ItemOwnership
                {
                    OwnerId = _workstationId,
                    OwnerType = OwnerType.Workstation,
                },
            }
        );
}
