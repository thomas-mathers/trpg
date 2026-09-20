using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class DeleteTriggersCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private DeleteTriggersCommandHandler _handler = null!;
    private readonly Trigger _trigger = Builders.MakeTrigger();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<DeleteTriggersCommandHandler>();
        _context.Props.Add(_trigger);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DeletesTheTrigger()
    {
        // Act
        await _handler.Handle(
            new DeleteTriggersCommand { TriggerIds = [_trigger.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.False(
            await verifyContext.Props.AnyAsync(
                prop => prop.Id == _trigger.Id,
                TestContext.Current.CancellationToken
            )
        );
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenGivenNoTriggerIds()
    {
        // Act
        await _handler.Handle(
            new DeleteTriggersCommand { TriggerIds = [] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        Assert.True(
            await verifyContext.Props.AnyAsync(
                prop => prop.Id == _trigger.Id,
                TestContext.Current.CancellationToken
            )
        );
    }
}
