using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

[Collection("Database")]
public sealed class PullLeverCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private PullLeverCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<PullLeverCommandHandler>();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_MarksTheLeverPulled_AndPersistsIt()
    {
        // Arrange
        var lever = Builders.MakeLever();
        _context.Props.Add(lever);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new PullLeverCommand { LeverId = lever.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updated = await verifyContext
            .Props.OfType<Lever>()
            .SingleAsync(l => l.Id == lever.Id, TestContext.Current.CancellationToken);
        Assert.True(updated.IsPulled);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyPulledFalse_OnTheFirstPull()
    {
        // Arrange
        var lever = Builders.MakeLever();
        _context.Props.Add(lever);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new PullLeverCommand { LeverId = lever.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.AlreadyPulled);
    }

    [Fact]
    public async Task Handle_ReturnsAlreadyPulledTrue_WhenPulledASecondTime()
    {
        // Arrange
        var lever = Builders.MakeLever(isPulled: true);
        _context.Props.Add(lever);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new PullLeverCommand { LeverId = lever.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.AlreadyPulled);
    }

    [Fact]
    public async Task Handle_ThrowsEntityNotFoundException_WhenTheLeverDoesNotExist()
    {
        // Act & Assert
        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            _handler.Handle(
                new PullLeverCommand { LeverId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            )
        );
    }
}
