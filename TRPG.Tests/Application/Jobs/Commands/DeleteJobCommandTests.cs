using Microsoft.EntityFrameworkCore;
using TRPG.Application.Jobs.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Commands;

[Collection("Database")]
public sealed class DeleteJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddJobCommandHandler _addJob = null!;
    private Creature _creature = null!;
    private TrpgDbContext _context = null!;
    private DeleteJobCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddJobCommandHandler(_context);
        _handler = new DeleteJobCommandHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemovesJob()
    {
        // Arrange
        var job = Builders.MakeJob(_creature.Id);
        await _addJob.Handle(
            new AddJobCommand { Job = job },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new DeleteJobCommand { Id = job.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var jobs = await verifyContext
            .Jobs.Where(j => j.Id == job.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(jobs);
    }
}
