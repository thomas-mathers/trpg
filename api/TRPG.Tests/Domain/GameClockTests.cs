using TRPG.Domain;

namespace TRPG.Tests.Domain;

public class GameClockTests
{
    [Fact]
    public void GetCurrentInGameDateTime_DoesNotAdvance_FromRealTimeAlone()
    {
        // Arrange
        var bankedGameTime = GameClock.Epoch;
        var before = GameClock.GetCurrentInGameDateTime(bankedGameTime);
        Thread.Sleep(500);

        // Act - real time passing on its own must not move the in-game clock
        var after = GameClock.GetCurrentInGameDateTime(bankedGameTime);

        // Assert
        Assert.Equal(before, after);
    }

    [Fact]
    public void GetCurrentInGameDateTime_UsesOneToOneDurationArithmetic()
    {
        // Arrange
        var bankedGameTime = GameClock.Epoch;
        var before = GameClock.GetCurrentInGameDateTime(bankedGameTime);

        var after = GameClock.GetCurrentInGameDateTime(bankedGameTime + TimeSpan.FromHours(5));

        // Assert
        Assert.Equal(TimeSpan.FromHours(5), after - before);
    }

    [Theory]
    [InlineData(1, Season.Winter)] // Frostwane
    [InlineData(2, Season.Winter)] // Coldmere
    [InlineData(3, Season.Spring)] // Thawmoon
    [InlineData(5, Season.Spring)] // Bloomrise
    [InlineData(6, Season.Summer)] // Suncrest
    [InlineData(8, Season.Summer)] // Emberfall
    [InlineData(9, Season.Autumn)] // Harvestide
    [InlineData(11, Season.Autumn)] // Graytide
    [InlineData(12, Season.Winter)] // Hearthwane
    public void GetCurrentSeason_MapsTheMonthToItsSeason(int month, Season expected)
    {
        var instant = new GameInstant(
            new DateTime(GameClock.EpochYear, month, 15, 8, 0, 0, DateTimeKind.Unspecified)
        );

        // Act
        var season = GameClock.GetCurrentSeason(instant);

        // Assert
        Assert.Equal(expected, season);
    }

    [Fact]
    public void Epoch_UsesTheFictionalStart()
    {
        var instant = GameClock.Epoch;

        Assert.Equal(new DateTime(975, 1, 1, 8, 0, 0), instant.Value);
        Assert.Equal(DateTimeKind.Unspecified, instant.Value.Kind);
    }

    [Fact]
    public void GetCurrentSeason_ReturnsSeason_ForGameInstant()
    {
        var instant = new GameInstant(new DateTime(975, 9, 1, 0, 0, 0));

        var season = GameClock.GetCurrentSeason(instant);

        Assert.Equal(Season.Autumn, season);
    }

    [Fact]
    public void GetCurrentInGameDate_ReturnsCalendarDate_ForGameInstant()
    {
        var instant = new GameInstant(new DateTime(975, 1, 2, 13, 0, 0));

        var date = GameClock.GetCurrentInGameDate(instant);

        Assert.Equal(975, date.Year);
        Assert.Equal("Frostwane", date.MonthName);
        Assert.Equal(2, date.Day);
        Assert.Equal("Ashday", date.WeekdayName);
        Assert.Equal(DayOfWeek.Monday, date.Weekday);
        Assert.Equal(13, date.Hour);
    }
}
