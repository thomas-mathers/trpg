using TRPG.Application.GameSessions;

namespace TRPG.Tests.Application.GameSessions;

public class GameClockTests
{
    [Fact]
    public void GetCurrentInGameDateTime_DoesNotAdvance_FromRealTimeAlone()
    {
        // Arrange
        var bankedPlaytime = TimeSpan.Zero;
        var before = GameClock.GetCurrentInGameDateTime(bankedPlaytime);
        Thread.Sleep(500);

        // Act — real time passing on its own must not move the in-game clock
        var after = GameClock.GetCurrentInGameDateTime(bankedPlaytime);

        // Assert
        Assert.Equal(before, after);
    }

    [Fact]
    public void RealTimePerInGameHour_ConvertsInGameHoursToRealTime_AtTheConfiguredRatio()
    {
        // Arrange
        var bankedPlaytime = TimeSpan.Zero;
        var before = GameClock.GetCurrentInGameDateTime(bankedPlaytime);

        // Act — 5 in-game hours at the 12-in-game-hours-per-real-hour ratio is 5/12 of a real hour
        var after = GameClock.GetCurrentInGameDateTime(
            bankedPlaytime + 5 * GameClock.RealTimePerInGameHour
        );

        // Assert
        Assert.Equal(TimeSpan.FromHours(5), after - before);
    }
}
