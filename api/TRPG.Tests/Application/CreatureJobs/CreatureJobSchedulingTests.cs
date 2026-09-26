using TRPG.Application.CreatureJobs;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs;

public class CreatureJobSchedulingTests
{
    private readonly Guid _creatureId = Guid.NewGuid();

    [Theory]
    [InlineData(8, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(7, false)]
    public void IsActiveAtHour_HandlesNormalWindow(int hour, bool expected)
    {
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 8, endHour: 20);

        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, hour);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(21, false)]
    public void IsActiveAtHour_HandlesMidnightWraparound(int hour, bool expected)
    {
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 22, endHour: 6);

        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, hour);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsFalse_WhenStartEqualsEnd()
    {
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 0, endHour: 0);

        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, 0);

        Assert.False(result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsTrue_WhenSpecificDayMatchesCurrentWeekday()
    {
        var job = Builders.MakeCreatureJob(
            _creatureId,
            startHour: 8,
            endHour: 20,
            specificDay: DayOfWeek.Monday
        );

        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, 10);

        Assert.True(result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsFalse_WhenSpecificDayDoesNotMatchCurrentWeekday()
    {
        var job = Builders.MakeCreatureJob(
            _creatureId,
            startHour: 8,
            endHour: 20,
            specificDay: DayOfWeek.Monday
        );

        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Tuesday, 10);

        Assert.False(result);
    }

    [Fact]
    public void FindCurrentOrNextJob_ReturnsActiveJobImmediately()
    {
        var gameTime = GameClock.Epoch;
        var currentDate = GameClock.GetCurrentInGameDateTime(gameTime);
        var job = Job(currentDate.Hour, currentDate.Hour + 1);

        var scheduled = CreatureJobScheduling.FindCurrentOrNextJob([job], gameTime);

        Assert.NotNull(scheduled);
        Assert.True(scheduled.IsActive);
        Assert.Equal(gameTime, scheduled.StartsAtGameTime);
    }

    [Fact]
    public void FindCurrentOrNextJob_ReturnsTheActualStartOfAnActiveJob()
    {
        var gameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 3;
        var currentDate = GameClock.GetCurrentInGameDateTime(gameTime);
        var job = Job(currentDate.Hour - 2, currentDate.Hour + 1);

        var scheduled = CreatureJobScheduling.FindCurrentOrNextJob([job], gameTime);

        Assert.NotNull(scheduled);
        Assert.True(scheduled.IsActive);
        Assert.Equal(gameTime - TimeSpan.FromHours(2), scheduled.StartsAtGameTime);
    }

    [Fact]
    public void FindMostRecentEndGameTime_ReturnsThePreviousOvernightShiftBoundary()
    {
        var gameTime = GameClock.Epoch + TimeSpan.FromHours(1) * 23;
        var job = Job(startHour: 22, endHour: 6);

        var end = CreatureJobScheduling.FindMostRecentEndGameTime(job, gameTime);

        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(22), end);
    }

    [Fact]
    public void FindCurrentOrNextJob_ReturnsUpcomingJobStart()
    {
        var gameTime = GameClock.Epoch;
        var currentDate = GameClock.GetCurrentInGameDateTime(gameTime);
        var job = Job(currentDate.Hour + 2, currentDate.Hour + 3);

        var scheduled = CreatureJobScheduling.FindCurrentOrNextJob([job], gameTime);

        Assert.NotNull(scheduled);
        Assert.False(scheduled.IsActive);
        Assert.Equal(gameTime + TimeSpan.FromHours(2), scheduled.StartsAtGameTime);
    }

    private static CreatureJob Job(int startHour, int endHour) =>
        new()
        {
            CreatureId = Guid.NewGuid(),
            WorldId = Guid.NewGuid(),
            LocationId = Guid.NewGuid(),
            StartHour = startHour,
            EndHour = endHour,
        };
}
