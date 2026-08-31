using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class AddCreaturesCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddCreaturesCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddCreaturesCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsAllCreatures()
    {
        // Arrange
        var first = Builders.MakeCreature();
        var second = Builders.MakeCreature();

        // Act
        await _handler.Handle(
            new AddCreaturesCommand { Creatures = [first, second] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.Creatures.AnyAsync(
                creature => creature.Id == first.Id,
                TestContext.Current.CancellationToken
            )
        );
        Assert.True(
            await verifyContext.Creatures.AnyAsync(
                creature => creature.Id == second.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
