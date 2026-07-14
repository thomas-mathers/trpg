using TRPG.Application.Game;

namespace TRPG.Tests.Application.Game;

public class GameClockTests
{
    [Fact]
    public void GetCurrentInGameDateTime_DoesNotAdvance_FromRealTimeAlone()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);
        var before = GameClock.GetCurrentInGameDateTime(session);

        // Act — real time passing on its own must not move the in-game clock
        Thread.Sleep(500);
        var after = GameClock.GetCurrentInGameDateTime(session);

        // Assert
        Assert.Equal(before, after);
    }

    [Fact]
    public void AdvanceHours_MovesTheInGameClockByExactlyTheRequestedAmount()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);
        var before = GameClock.GetCurrentInGameDateTime(session);

        // Act
        GameClock.AdvanceHours(session, 5);
        var after = GameClock.GetCurrentInGameDateTime(session);

        // Assert
        Assert.Equal(TimeSpan.FromHours(5), after - before);
    }

    [Fact]
    public void AdvanceForMessage_ConvertsRealSecondsToInGameTime_AtTheConfiguredRatio()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);
        var before = GameClock.GetCurrentInGameDateTime(session);

        // Act — 60 real seconds at the 12-in-game-hours-per-real-hour ratio is 12 in-game minutes
        GameClock.AdvanceForMessage(session, realTimePerMessage: TimeSpan.FromSeconds(60));
        var after = GameClock.GetCurrentInGameDateTime(session);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(12), after - before);
    }
}
