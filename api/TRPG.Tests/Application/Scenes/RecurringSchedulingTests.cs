using TRPG.Application.Scenes;
using TRPG.Domain;

namespace TRPG.Tests.Application.Scenes;

public class RecurringSchedulingTests
{
    // 2 real hours = 24 in-game hours = exactly one in-game day, at the fixed 12x game clock rate.
    private static readonly TimeSpan OneInGameDay = TimeSpan.FromHours(2);

    [Fact]
    public void HasTriggered_ReturnsFalse_WhenNoPlaytimeHasElapsed()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 6,
            specificDay: null,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: TimeSpan.Zero
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_ForADailySchedule_WhenLessThanADayHasElapsed()
    {
        // Act — half an in-game day has passed, not enough to cross a new daily trigger
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 6,
            specificDay: null,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: OneInGameDay / 2
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForADailySchedule_WhenAFullDayHasElapsed()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 6,
            specificDay: null,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: OneInGameDay
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_ForAWeeklySchedule_WhenLessThanAWeekHasElapsed()
    {
        // Arrange — trigger on the same weekday the epoch falls on
        var epochWeekday = GameClock.GetCurrentInGameDateTime(TimeSpan.Zero).DayOfWeek;

        // Act — 6 in-game days have passed, one short of a full week
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 0,
            specificDay: epochWeekday,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: OneInGameDay * 6
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForAWeeklySchedule_WhenAFullWeekHasElapsed()
    {
        // Arrange
        var epochWeekday = GameClock.GetCurrentInGameDateTime(TimeSpan.Zero).DayOfWeek;

        // Act
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 0,
            specificDay: epochWeekday,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: OneInGameDay * 7
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_ForAWeeklySchedule_WhenADayElapsedButNotOnTheSpecificDay()
    {
        // Arrange — trigger on the day *after* the epoch's weekday, so one elapsed day never matches
        var epochWeekday = GameClock.GetCurrentInGameDateTime(TimeSpan.Zero).DayOfWeek;
        var nextWeekday = (DayOfWeek)(((int)epochWeekday + 2) % 7);

        // Act
        var result = RecurringScheduling.HasTriggered(
            triggerHour: 0,
            specificDay: nextWeekday,
            lastSyncPlaytime: TimeSpan.Zero,
            currentPlaytime: OneInGameDay
        );

        // Assert
        Assert.False(result);
    }
}
