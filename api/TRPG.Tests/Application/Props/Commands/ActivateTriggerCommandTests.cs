using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class ActivateTriggerCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ActivateTriggerCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<ActivateTriggerCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MarksTheTriggerActivated_AndPersistsIt()
    {
        // Arrange
        var trigger = Builders.MakeTrigger();
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext
            .Props.OfType<Trigger>()
            .SingleAsync(t => t.Id == trigger.Id, TestContext.Current.CancellationToken);
        Assert.True(updated.IsActivated);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyActivatedFalse_OnTheFirstActivation()
    {
        // Arrange
        var trigger = Builders.MakeTrigger();
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.AlreadyActivated);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyActivatedTrue_WhenActivatedASecondTime()
    {
        // Arrange
        var trigger = Builders.MakeTrigger(isActivated: true);
        _context.Props.Add(trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new ActivateTriggerCommand { TriggerId = trigger.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.AlreadyActivated);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFoundException_WhenTheTriggerDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                new ActivateTriggerCommand { TriggerId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }
}
