using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Commands;

[Collection("Database")]
public sealed class AddJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private Creature _creature = null!;
    private TrpgDbContext _context = null!;
    private GetAllJobsByCreatureIdQueryHandler _getAllByCreatureId = null!;
    private AddJobCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AddJobCommandHandler(_context);
        _getAllByCreatureId = new GetAllJobsByCreatureIdQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsJob()
    {
        // Arrange
        var job = Builders.MakeJob(_creature.Id);

        // Act
        await _handler.Handle(
            new AddJobCommand { Job = job },
            TestContext.Current.CancellationToken
        );

        // Assert
        var jobs = await _getAllByCreatureId.Handle(
            new GetAllJobsByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(jobs, j => j.Id == job.Id);
    }
}
