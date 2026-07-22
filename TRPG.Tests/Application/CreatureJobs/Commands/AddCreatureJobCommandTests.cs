using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class AddCreatureJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private GetAllCreatureJobsByCreatureIdQueryHandler _getAllByCreatureId = null!;
    private AddCreatureJobCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new AddCreatureJobCommandHandler(_context);
        _getAllByCreatureId = new GetAllCreatureJobsByCreatureIdQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsJob()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creature.Id);

        // Act
        await _handler.Handle(
            new AddCreatureJobCommand { CreatureJob = job },
            TestContext.Current.CancellationToken
        );

        // Assert
        var jobs = await _getAllByCreatureId.Handle(
            new GetAllCreatureJobsByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );
        Assert.Contains(jobs, j => j.Id == job.Id);
    }
}
