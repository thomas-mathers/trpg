using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

public sealed class TryOccupyAnyAvailableWorkstationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private TryOccupyAnyAvailableWorkstationCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<TryOccupyAnyAvailableWorkstationCommandHandler>();
        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SkipsOccupiedAndReadingWorkstationsBeforeClaimingAUsableOne()
    {
        var occupied = Builders.MakeWorkstation(_worldId, _locationId, occupantId: Guid.NewGuid());
        var reading = Builders.MakeWorkstation(
            _worldId,
            _locationId,
            workstationType: WorkstationType.Reading
        );
        var available = Builders.MakeWorkstation(_worldId, _locationId);
        _context.Props.AddRange(occupied, reading, available);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var workerId = Guid.NewGuid();

        var claimed = await _handler.Handle(
            new TryOccupyAnyAvailableWorkstationCommand
            {
                LocationId = _locationId,
                CreatureId = workerId,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(available.Id, claimed);
        var updated = await _context
            .Props.OfType<Workstation>()
            .AsNoTracking()
            .SingleAsync(
                workstation => workstation.Id == available.Id,
                TestContext.Current.CancellationToken
            );
        Assert.Equal(workerId, updated.OccupantId);
    }
}
