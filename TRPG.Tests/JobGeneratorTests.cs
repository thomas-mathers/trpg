using TRPG.Generators;
using TRPG.Models;

namespace TRPG.Tests;

public class JobGeneratorTests {
    [Fact]
    public void Generate_AlwaysIncludesSleepAndIdle() {
        // Arrange
        var stateId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var idleRoomId = Guid.NewGuid();

        // Act
        var jobs = JobGenerator.Generate(stateId, personId, sleepRoomId, null, idleRoomId, Guid.NewGuid());

        // Assert
        var sleep = Assert.Single(jobs, j => j.Action == JobAction.Sleep);
        Assert.Equal(22, sleep.StartHour);
        Assert.Equal(6, sleep.EndHour);
        Assert.Equal(100, sleep.Priority);
        Assert.Equal(sleepRoomId, sleep.RoomId);

        var idle = Assert.Single(jobs, j => j.Action == JobAction.Idle);
        Assert.Equal(6, idle.StartHour);
        Assert.Equal(22, idle.EndHour);
        Assert.Equal(0, idle.Priority);
        Assert.Equal(idleRoomId, idle.RoomId);
    }

    [Fact]
    public void Generate_OmitsWorkJob_WhenWorkRoomIdIsNull() {
        // Arrange
        var stateId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();

        // Act
        var jobs = JobGenerator.Generate(stateId, personId, sleepRoomId, null, null, Guid.NewGuid());

        // Assert
        Assert.DoesNotContain(jobs, j => j.Action == JobAction.Work);
        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public void Generate_IncludesWorkJob_WhenWorkRoomIdProvided() {
        // Arrange
        var stateId = Guid.NewGuid();
        var personId = Guid.NewGuid();
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();

        // Act
        var jobs = JobGenerator.Generate(stateId, personId, sleepRoomId, workRoomId, workRoomId, Guid.NewGuid());

        // Assert
        var work = Assert.Single(jobs, j => j.Action == JobAction.Work);
        Assert.Equal(8, work.StartHour);
        Assert.Equal(20, work.EndHour);
        Assert.Equal(50, work.Priority);
        Assert.Equal(workRoomId, work.RoomId);
        Assert.Equal(3, jobs.Count);
    }
}
