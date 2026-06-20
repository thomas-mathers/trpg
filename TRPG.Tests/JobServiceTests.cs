using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class JobServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private JobService _service = null!;
    private Person _person = null!;
    private Job _job = null!;

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new JobService(_context);

        _person = Builders.MakePerson();
        _context.Persons.Add(_person);
        await _context.SaveChangesAsync();

        _job = Builders.MakeJob(_person.Id);
        await _service.Add(_job);
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task Add_PersistsJob()
    {
        // Arrange
        var job = Builders.MakeJob(_person.Id);

        // Act
        await _service.Add(job);

        // Assert
        var jobs = await _service.GetAllByPersonId(_person.Id);
        Assert.Contains(jobs, j => j.Id == job.Id);
    }

    [Fact]
    public async Task GetAllByPersonId_ReturnsJobsOrderedByPriorityDescending()
    {
        // Arrange
        var low = Builders.MakeJob(_person.Id, priority: 1);
        var high = Builders.MakeJob(_person.Id, priority: 10);
        var mid = Builders.MakeJob(_person.Id, priority: 5);
        await _service.Add(low);
        await _service.Add(high);
        await _service.Add(mid);

        // Act
        var jobs = await _service.GetAllByPersonId(_person.Id);

        // Assert — seeded _job (priority 1) plus three new ones; highest priority first
        Assert.Equal(high.Id, jobs[0].Id);
        Assert.Equal(mid.Id, jobs[1].Id);
    }

    [Fact]
    public async Task Update_SavesChanges()
    {
        // Arrange — build updated entity in a fresh context to avoid tracking conflict with _job
        var newWorldId = Guid.NewGuid();
        var updated = new Job
        {
            Id = _job.Id,
            PersonId = _job.PersonId,
            Action = _job.Action,
            StartHour = _job.StartHour,
            EndHour = _job.EndHour,
            Daily = _job.Daily,
            Priority = _job.Priority,
            Location = new Location { WorldId = newWorldId, Coordinates = new Point(0, 0) }
        };

        // Act
        await using var updateContext = db.CreateContext();
        await new JobService(updateContext).Update(updated);

        // Assert
        await using var verifyContext = db.CreateContext();
        var found = await verifyContext.Jobs.FirstAsync(j => j.Id == _job.Id);
        Assert.Equal(newWorldId, found.Location.WorldId);
    }

    [Fact]
    public async Task Delete_RemovesJob()
    {
        // Arrange
        var job = Builders.MakeJob(_person.Id);
        await _service.Add(job);

        // Act
        await _service.Delete(job.Id);

        // Assert
        await using var verifyContext = db.CreateContext();
        var jobs = await verifyContext.Jobs.Where(j => j.Id == job.Id).ToListAsync();
        Assert.Empty(jobs);
    }
}
