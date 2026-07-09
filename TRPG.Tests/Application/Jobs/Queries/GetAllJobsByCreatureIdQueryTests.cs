using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Queries;

[Collection("Database")]
public sealed class GetAllJobsByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddJobCommandHandler _addJob = null!;
    private Creature _creature = null!;
    private TrpgDbContext _context = null!;
    private GetAllJobsByCreatureIdQueryHandler _handler = null!;
    private Job _job = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddJobCommandHandler(_context);
        _handler = new GetAllJobsByCreatureIdQueryHandler(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();

        _job = Builders.MakeJob(_creature.Id);
        await _addJob.Handle(
            new AddJobCommand { Job = _job },
            TestContext.Current.CancellationToken
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsJobsOrderedByPriorityDescending()
    {
        // Arrange
        var low = Builders.MakeJob(_creature.Id);
        var high = Builders.MakeJob(_creature.Id, 10);
        var mid = Builders.MakeJob(_creature.Id, 5);
        await _addJob.Handle(
            new AddJobCommand { Job = low },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand { Job = high },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddJobCommand { Job = mid },
            TestContext.Current.CancellationToken
        );

        // Act
        var jobs = await _handler.Handle(
            new GetAllJobsByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert — seeded _job (priority 1) plus three new ones; highest priority first
        Assert.Equal(high.Id, jobs[0].Id);
        Assert.Equal(mid.Id, jobs[1].Id);
    }
}
