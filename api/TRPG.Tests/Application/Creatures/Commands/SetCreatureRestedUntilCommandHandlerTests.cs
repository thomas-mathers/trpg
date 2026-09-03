using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class SetCreatureRestedUntilCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SetCreatureRestedUntilCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SetCreatureRestedUntilCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SetsTheRestedUntilPlaytime()
    {
        // Arrange
        var restedUntil = TimeSpan.FromHours(24);

        // Act
        await _handler.Handle(
            new SetCreatureRestedUntilCommand
            {
                CreatureId = _creature.Id,
                RestedUntilPlaytime = restedUntil,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(restedUntil, updatedCreature.RestedUntilPlaytime);
    }
}
