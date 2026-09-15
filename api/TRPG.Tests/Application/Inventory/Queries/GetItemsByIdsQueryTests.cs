using TRPG.Application.Inventory.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory.Queries;

public sealed class GetItemsByIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private GetItemsByIdsQueryHandler _handler = null!;
    private readonly Item _itemA = Builders.MakeItem();
    private readonly Item _itemB = Builders.MakeItem();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetItemsByIdsQueryHandler(_context);

        _context.Items.AddRange(_itemA, _itemB);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenNoIdsMatch()
    {
        // Act
        var result = await _handler.Handle(
            new GetItemsByIdsQuery { Ids = [Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsAllMatchingItems()
    {
        // Act
        var result = await _handler.Handle(
            new GetItemsByIdsQuery { Ids = [_itemA.Id, _itemB.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(_itemA.Id));
        Assert.True(result.ContainsKey(_itemB.Id));
    }
}
