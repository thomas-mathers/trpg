using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Props.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Props.Commands;

[Collection("Database")]
public sealed class SetBedOccupantCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private SetBedOccupantCommandHandler _handler = null!;
    private readonly Bed _bed = Builders.MakeBed(WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<SetBedOccupantCommandHandler>();

        _context.Props.Add(_bed);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SetsTheOccupantId()
    {
        // Arrange
        var creatureId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new SetBedOccupantCommand { BedId = _bed.Id, OccupantId = creatureId },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == _bed.Id, TestContext.Current.CancellationToken);
        Assert.Equal(creatureId, updatedBed.OccupantId);
    }

    [Fact]
    public async Task Handle_ClearsTheOccupantId_WhenPassedNull()
    {
        // Arrange
        await _handler.Handle(
            new SetBedOccupantCommand { BedId = _bed.Id, OccupantId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new SetBedOccupantCommand { BedId = _bed.Id, OccupantId = null },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBed = await verifyContext
            .Props.OfType<Bed>()
            .SingleAsync(b => b.Id == _bed.Id, TestContext.Current.CancellationToken);
        Assert.Null(updatedBed.OccupantId);
    }
}
