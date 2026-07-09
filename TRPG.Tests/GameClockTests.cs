namespace TRPG.Tests;

public class GameClockTests
{
    [Fact]
    public void GetCurrentInGameDateTime_AdvancesAtConfiguredRatio()
    {
        // Arrange
        var session = new GameSession(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);
        var before = GameClock.GetCurrentInGameDateTime(session);

        // Act — sleep a known real duration and measure in-game elapsed time
        Thread.Sleep(2000);
        var after = GameClock.GetCurrentInGameDateTime(session);

        // Assert — 2 real seconds * 20 in-game-hours-per-real-hour ≈ 40 in-game seconds
        var elapsedInGameSeconds = (after - before).TotalSeconds;
        Assert.InRange(elapsedInGameSeconds, 30, 60);
    }
}
