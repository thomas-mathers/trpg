using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs.Commands;

[Collection("Database")]
public sealed class AddCreatureJobsCommandTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AddCreatureJobsCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature();

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AddCreatureJobsCommandHandler>();

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_AddsAllJobs()
    {
        // Arrange
        var sleepJob = Builders.MakeCreatureJob(_creature.Id, action: CreatureJobAction.Sleep);
        var workJob = Builders.MakeCreatureJob(_creature.Id, action: CreatureJobAction.Work);

        // Act
        await _handler.Handle(
            new AddCreatureJobsCommand { Jobs = [sleepJob, workJob] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var jobs = await verifyContext
            .CreatureJobs.Where(job => job.CreatureId == _creature.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, job => job.Action == CreatureJobAction.Sleep);
        Assert.Contains(jobs, job => job.Action == CreatureJobAction.Work);
    }
}
