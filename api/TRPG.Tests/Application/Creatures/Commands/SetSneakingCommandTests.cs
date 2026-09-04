using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

[Collection("Database")]
public sealed class SetSneakingCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SetSneakingCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SetSneakingCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SetsIsSneakingToTrue()
    {
        // Act
        await _handler.Handle(
            new SetSneakingCommand { CreatureId = _creature.Id, IsSneaking = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.True(updatedCreature.IsSneaking);
    }

    [Fact]
    public async Task Handle_SetsIsSneakingToFalse_WhenAlreadySneaking()
    {
        // Arrange
        _creature.IsSneaking = true;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SetSneakingCommand { CreatureId = _creature.Id, IsSneaking = false },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedCreature.IsSneaking);
    }
}
