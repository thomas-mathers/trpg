using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class JobServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private TrpgDbContext _context = null!;
    private Creature _creature = null!;
    private Job _job = null!;
    private JobService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new JobService(_context);

        _creature = Builders.MakeCreature();
        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync();

        _job = Builders.MakeJob(_creature.Id);
        await _service.Add(_job);
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Add_PersistsJob() {
        // Arrange
        var job = Builders.MakeJob(_creature.Id);

        // Act
        await _service.Add(job, TestContext.Current.CancellationToken);

        // Assert
        var jobs = await _service.GetAllByCreatureId(_creature.Id, TestContext.Current.CancellationToken);
        Assert.Contains(jobs, j => j.Id == job.Id);
    }

    [Fact]
    public async Task GetAllByCreatureId_ReturnsJobsOrderedByPriorityDescending() {
        // Arrange
        var low = Builders.MakeJob(_creature.Id);
        var high = Builders.MakeJob(_creature.Id, 10);
        var mid = Builders.MakeJob(_creature.Id, 5);
        await _service.Add(low, TestContext.Current.CancellationToken);
        await _service.Add(high, TestContext.Current.CancellationToken);
        await _service.Add(mid, TestContext.Current.CancellationToken);

        // Act
        var jobs = await _service.GetAllByCreatureId(_creature.Id, TestContext.Current.CancellationToken);

        // Assert — seeded _job (priority 1) plus three new ones; highest priority first
        Assert.Equal(high.Id, jobs[0].Id);
        Assert.Equal(mid.Id, jobs[1].Id);
    }

    [Fact]
    public async Task Update_SavesChanges() {
        // Arrange — build updated entity in a fresh context to avoid tracking conflict with _job
        var newStateId = Guid.NewGuid();
        var updated = new Job {
            Id = _job.Id,
            CreatureId = _job.CreatureId,
            Action = _job.Action,
            StartHour = _job.StartHour,
            EndHour = _job.EndHour,
            Daily = _job.Daily,
            Priority = _job.Priority,
            StateId = newStateId
        };

        // Act
        await using var updateContext = db.CreateContext();
        await new JobService(updateContext).Update(updated, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Jobs.FirstAsync(j => j.Id == _job.Id, TestContext.Current.CancellationToken);
        Assert.Equal(newStateId, found.StateId);
    }

    [Fact]
    public async Task GetCreatureIdsByRoomId_ReturnsDistinctCreatureIds() {
        // Arrange
        var roomId = Guid.NewGuid();
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.Add(Builders.MakeJob(_creature.Id, action: JobAction.Sleep, roomId: roomId),
            TestContext.Current.CancellationToken);
        await _service.Add(Builders.MakeJob(_creature.Id, action: JobAction.Work, roomId: roomId),
            TestContext.Current.CancellationToken);
        await _service.Add(Builders.MakeJob(otherCreature.Id, action: JobAction.Sleep, roomId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Act
        var creatureIds = await _service.GetCreatureIdsByRoomId(roomId, TestContext.Current.CancellationToken);

        // Assert
        var id = Assert.Single(creatureIds);
        Assert.Equal(_creature.Id, id);
    }

    [Fact]
    public async Task Delete_RemovesJob() {
        // Arrange
        var job = Builders.MakeJob(_creature.Id);
        await _service.Add(job, TestContext.Current.CancellationToken);

        // Act
        await _service.Delete(job.Id, TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var jobs = await verifyContext.Jobs.Where(j => j.Id == job.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Empty(jobs);
    }
}
