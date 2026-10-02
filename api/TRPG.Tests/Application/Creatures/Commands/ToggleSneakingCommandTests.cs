using Anthropic.Models.Beta.Sessions.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class ToggleSneakingCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ToggleSneakingCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ToggleSneakingCommandHandler>();

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
        var result = await _handler.Handle(
            new ToggleSneakingCommand { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_creature.Id, result.CreatureId);
        Assert.True(result.IsSneaking);

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
        var result = await _handler.Handle(
            new ToggleSneakingCommand { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_creature.Id, result.CreatureId);
        Assert.False(result.IsSneaking);

        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(updatedCreature.IsSneaking);
    }

    [Fact]
    public async Task Handle_HalvesMovementSpeed_WhenSneakingStarts()
    {
        // Arrange
        var options = new CreatureGeneratorOptions();
        var expected = StatFormulas.CalculateTravelSpeed(
            _creature.BaseAttributes.Dexterity,
            [],
            true,
            options
        );
        _creature.IsSneaking = false;
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ToggleSneakingCommand { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(_creature.Id, result.CreatureId);
        Assert.True(result.IsSneaking);
        Assert.Equal(expected, result.MovementSpeed);

        await using var verifyContext = db.CreateContext();
        var updatedCreature = await verifyContext.Creatures.SingleAsync(
            c => c.Id == _creature.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(expected, updatedCreature.MovementSpeed);
    }
}
