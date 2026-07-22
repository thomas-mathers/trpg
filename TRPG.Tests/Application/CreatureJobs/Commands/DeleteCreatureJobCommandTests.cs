using Microsoft.EntityFrameworkCore;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;
using TRPG.Tests.Helpers.Extensions;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class DeleteCreatureJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private DeleteCreatureJobCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddCreatureJobCommandHandler(_context);
        _handler = new DeleteCreatureJobCommandHandler(_context);

        await _context.AddCreature(_creature, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemovesJob()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creature.Id);
        await _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = job },
            TestContext.Current.CancellationToken
        );

        // Act
        await _handler.Handle(
            new DeleteCreatureJobCommand { Id = job.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var jobs = await verifyContext
            .CreatureJobs.Where(j => j.Id == job.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(jobs);
    }
}
