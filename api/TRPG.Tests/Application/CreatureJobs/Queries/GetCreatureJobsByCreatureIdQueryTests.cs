using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Queries;

[Collection("Database")]
public sealed class GetCreatureJobsByCreatureIdQueryTests(DatabaseFixture db) : IAsyncLifetime
{
    private AddCreatureJobCommandHandler _addJob = null!;
    private TrpgDbContext _context = null!;
    private GetCreatureJobsByCreatureIdQueryHandler _handler = null!;
    private CreatureJob _creatureJob = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _addJob = new AddCreatureJobCommandHandler(_context);
        _handler = new GetCreatureJobsByCreatureIdQueryHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _creatureJob = Builders.MakeCreatureJob(_creature.Id);
        await _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = _creatureJob },
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
        var low = Builders.MakeCreatureJob(_creature.Id);
        var high = Builders.MakeCreatureJob(_creature.Id, 10);
        var mid = Builders.MakeCreatureJob(_creature.Id, 5);
        await _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = low },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = high },
            TestContext.Current.CancellationToken
        );
        await _addJob.Handle(
            new AddCreatureJobCommand { CreatureJob = mid },
            TestContext.Current.CancellationToken
        );

        // Act
        var jobs = await _handler.Handle(
            new GetCreatureJobsByCreatureIdQuery { CreatureId = _creature.Id },
            TestContext.Current.CancellationToken
        );

        // Assert — seeded _job (priority 1) plus three new ones; highest priority first
        Assert.Equal(high.Id, jobs[0].Id);
        Assert.Equal(mid.Id, jobs[1].Id);
    }
}
