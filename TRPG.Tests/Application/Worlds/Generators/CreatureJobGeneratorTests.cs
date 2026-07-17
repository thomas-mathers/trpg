using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class CreatureJobGeneratorTests
{
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _personId = Guid.NewGuid();
    private readonly Guid _worldId = Guid.NewGuid();

    [Fact]
    public void Generate_AlwaysIncludesSleepAndIdle()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var idleRoomId = Guid.NewGuid();

        // Act
        var jobs = CreatureJobGenerator.Generate(
            _stateId,
            _personId,
            sleepRoomId,
            null,
            idleRoomId,
            _worldId
        );

        // Assert
        var sleep = Assert.Single(jobs, j => j.Action == CreatureJobAction.Sleep);
        Assert.Equal(22, sleep.StartHour);
        Assert.Equal(6, sleep.EndHour);
        Assert.Equal(100, sleep.Priority);
        Assert.Equal(sleepRoomId, sleep.RoomId);

        var idle = Assert.Single(jobs, j => j.Action == CreatureJobAction.Idle);
        Assert.Equal(6, idle.StartHour);
        Assert.Equal(22, idle.EndHour);
        Assert.Equal(0, idle.Priority);
        Assert.Equal(idleRoomId, idle.RoomId);
    }

    [Fact]
    public void Generate_OmitsWorkJob_WhenWorkRoomIdIsNull()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();

        // Act
        var jobs = CreatureJobGenerator.Generate(
            _stateId,
            _personId,
            sleepRoomId,
            null,
            null,
            _worldId
        );

        // Assert
        Assert.DoesNotContain(jobs, j => j.Action == CreatureJobAction.Work);
        Assert.Equal(2, jobs.Count);
    }

    [Fact]
    public void Generate_IncludesWorkJob_WhenWorkRoomIdProvided()
    {
        // Arrange
        var sleepRoomId = Guid.NewGuid();
        var workRoomId = Guid.NewGuid();

        // Act
        var jobs = CreatureJobGenerator.Generate(
            _stateId,
            _personId,
            sleepRoomId,
            workRoomId,
            workRoomId,
            _worldId
        );

        // Assert
        var work = Assert.Single(jobs, j => j.Action == CreatureJobAction.Work);
        Assert.Equal(8, work.StartHour);
        Assert.Equal(20, work.EndHour);
        Assert.Equal(50, work.Priority);
        Assert.Equal(workRoomId, work.RoomId);
        Assert.Equal(3, jobs.Count);
    }

    [Fact]
    public void GenerateDayOff_MatchesWorkHoursAndOutranksWork()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        var job = CreatureJobGenerator.GenerateDayOff(
            _stateId,
            _personId,
            CreatureJobAction.Sit,
            roomId,
            DayOfWeek.Saturday,
            _worldId
        );

        // Assert
        Assert.Equal(CreatureJobAction.Sit, job.Action);
        Assert.Equal(8, job.StartHour);
        Assert.Equal(20, job.EndHour);
        Assert.Equal(DayOfWeek.Saturday, job.SpecificDay);
        Assert.Equal(roomId, job.RoomId);
        Assert.True(
            job.Priority > 50,
            "Day-off jobs must outrank Work (50) to actually override it."
        );
    }

    [Fact]
    public void GenerateUnemployedDayActivity_MatchesIdleHoursAndOutranksIdle()
    {
        // Arrange
        var roomId = Guid.NewGuid();

        // Act
        var job = CreatureJobGenerator.GenerateUnemployedDayActivity(
            _stateId,
            _personId,
            CreatureJobAction.Study,
            roomId,
            DayOfWeek.Tuesday,
            _worldId
        );

        // Assert
        Assert.Equal(CreatureJobAction.Study, job.Action);
        Assert.Equal(6, job.StartHour);
        Assert.Equal(22, job.EndHour);
        Assert.Equal(DayOfWeek.Tuesday, job.SpecificDay);
        Assert.Equal(roomId, job.RoomId);
        Assert.True(
            job.Priority > 0,
            "Unemployed day activities must outrank the default Idle (0) to apply."
        );
    }
}
