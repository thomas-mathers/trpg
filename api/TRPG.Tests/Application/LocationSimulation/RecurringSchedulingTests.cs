using TRPG.Application.LocationSimulation;
using TRPG.Domain;

namespace TRPG.Tests.Application.LocationSimulation;

public class RecurringSchedulingTests
{
    private static readonly TimeSpan OneInGameDay = TimeSpan.FromHours(1) * 24;

    [Fact]
    public void HasTriggered_ReturnsFalse_WhenNoTimeHasPassed()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            "0 0 * * *",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForADailySchedule_WhenADayHasPassed()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            "0 0 * * *",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_ForAnEveryOtherDaySchedule_AfterOnlyOneDay()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            "0 0 */2 * *",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay
        );

        // Assert — somewhere cleared out stays cleared until its day comes round.
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForAnEveryOtherDaySchedule_AfterTwoDays()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            "0 0 */2 * *",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay * 2
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForAnHourlySchedule_WithinASingleDay()
    {
        // Act — a cadence the old trigger-hour and weekday pair could not express at all.
        var result = RecurringScheduling.HasTriggered(
            "0 */6 * * *",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + TimeSpan.FromHours(1) * 7
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_ForAWeeklySchedule_WhenOnlyADayHasPassed()
    {
        // Arrange — pin the weekday to two days after the epoch's, so one day never reaches it.
        var epochWeekday = GameClock.GetCurrentInGameDateTime(GameClock.Epoch).DayOfWeek;
        var target = (int)(DayOfWeek)(((int)epochWeekday + 2) % 7);

        // Act
        var result = RecurringScheduling.HasTriggered(
            $"0 0 * * {target}",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HasTriggered_ReturnsTrue_ForAWeeklySchedule_OnceTheWeekdayComesRound()
    {
        // Arrange
        var epochWeekday = GameClock.GetCurrentInGameDateTime(GameClock.Epoch).DayOfWeek;
        var target = (int)(DayOfWeek)(((int)epochWeekday + 2) % 7);

        // Act
        var result = RecurringScheduling.HasTriggered(
            $"0 0 * * {target}",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay * 3
        );

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasTriggered_ReturnsFalse_WhenTheScheduleCannotBeParsed()
    {
        // Act
        var result = RecurringScheduling.HasTriggered(
            "not a schedule",
            lastSyncGameTime: GameClock.Epoch,
            currentGameTime: GameClock.Epoch + OneInGameDay * 30
        );

        // Assert — a broken schedule must not fire constantly; see the validation gap it leaves.
        Assert.False(result);
    }
}
