using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreatureJobGeneratorTests
{
    private static List<CreatureJob> SeedDefaultSleepJob(Guid creatureId, Guid worldId)
    {
        return [CreatureJobGenerator.GenerateSleep(creatureId, Guid.NewGuid(), worldId)];
    }

    [Fact]
    public void ApplySleepOverride_WorkHoursDoNotOverlapDefaultSleep_LeavesSleepJobUnchanged()
    {
        // Arrange
        var creatureId = Guid.NewGuid();
        var worldId = Guid.NewGuid();
        var jobs = SeedDefaultSleepJob(creatureId, worldId);
        var originalSleepJob = jobs[0];

        // Act
        CreatureJobGenerator.ApplySleepOverride(creatureId, new HourWindow(7, 17), worldId, jobs);

        // Assert
        Assert.Same(originalSleepJob, Assert.Single(jobs));
    }

    [Fact]
    public void ApplySleepOverride_NightShiftOverlappingDefaultSleep_ReplacesSleepWithEightHourBlockAfterWork()
    {
        // Arrange
        var creatureId = Guid.NewGuid();
        var worldId = Guid.NewGuid();
        var jobs = SeedDefaultSleepJob(creatureId, worldId);

        // Act — Tavern's work hours (16-4), which cross midnight through the default sleep window
        CreatureJobGenerator.ApplySleepOverride(creatureId, new HourWindow(16, 4), worldId, jobs);

        // Assert
        var sleepJob = Assert.Single(jobs);
        Assert.Equal(4, sleepJob.StartHour);
        Assert.Equal(12, sleepJob.EndHour);
    }

    [Fact]
    public void ApplySleepOverride_OverlappingWorkHours_PreservesOriginalSleepLocation()
    {
        // Arrange
        var creatureId = Guid.NewGuid();
        var worldId = Guid.NewGuid();
        var jobs = SeedDefaultSleepJob(creatureId, worldId);
        var originalLocationId = jobs[0].LocationId;

        // Act
        CreatureJobGenerator.ApplySleepOverride(creatureId, new HourWindow(18, 6), worldId, jobs);

        // Assert
        Assert.Equal(originalLocationId, Assert.Single(jobs).LocationId);
    }
}
