using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class AddTriggersCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddTriggersCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddTriggersCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsAllTriggers()
    {
        // Arrange
        var first = Builders.MakeTrigger();
        var second = Builders.MakeTrigger();

        // Act
        await _handler.Handle(
            new AddTriggersCommand { Triggers = [first, second] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.Props.AnyAsync(
                prop => prop.Id == first.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Props.AnyAsync(
                prop => prop.Id == second.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
