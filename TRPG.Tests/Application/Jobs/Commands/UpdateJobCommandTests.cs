using Microsoft.EntityFrameworkCore;
using TRPG.Application.Jobs.Commands;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Jobs.Commands;

[Collection("Database")]
public sealed class UpdateJobCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private Creature _creature = null!;
    private TrpgDbContext _context = null!;
    private Job _job = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();

        _job = Builders.MakeJob(_creature.Id);
        await new AddJobCommandHandler(_context).Handle(
            new AddJobCommand { Job = _job },
            TestContext.Current.CancellationToken
        );
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_SavesChanges()
    {
        // Arrange — build updated entity in a fresh context to avoid tracking conflict with _job
        var newStateId = Guid.NewGuid();
        var updated = new Job
        {
            Id = _job.Id,
            CreatureId = _job.CreatureId,
            Action = _job.Action,
            StartHour = _job.StartHour,
            EndHour = _job.EndHour,
            SpecificDay = _job.SpecificDay,
            Priority = _job.Priority,
            StateId = newStateId,
        };

        // Act
        await using var updateContext = db.CreateContext();
        await new UpdateJobCommandHandler(updateContext).Handle(
            new UpdateJobCommand { Job = updated },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Jobs.FirstAsync(
            j => j.Id == _job.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(newStateId, found.StateId);
    }
}
