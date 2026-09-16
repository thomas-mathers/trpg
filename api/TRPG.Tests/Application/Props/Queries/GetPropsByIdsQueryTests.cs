using TRPG.Application.Props.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Queries;

public sealed class GetPropsByIdsQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private GetPropsByIdsQueryHandler _handler = null!;
    private readonly Container _container = Builders.MakeContainer(Guid.NewGuid(), Guid.NewGuid());
    private readonly Workstation _workstation = Builders.MakeWorkstation();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new GetPropsByIdsQueryHandler(_context);

        _context.Props.AddRange(_container, _workstation);
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
            new GetPropsByIdsQuery { Ids = [Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsAllMatchingPropsAcrossTypes()
    {
        // Act
        var result = await _handler.Handle(
            new GetPropsByIdsQuery { Ids = [_container.Id, _workstation.Id, Guid.NewGuid()] },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey(_container.Id));
        Assert.True(result.ContainsKey(_workstation.Id));
    }
}
