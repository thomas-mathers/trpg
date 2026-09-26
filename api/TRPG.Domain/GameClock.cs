using System.Globalization;
using TRPG.Domain.Models;

namespace TRPG.Domain;

public enum Season
{
    Winter,
    Spring,
    Summer,
    Autumn,
}

public static class GameClock
{
    public const int EpochYear = 975;
    public static GameInstant Epoch { get; } =
        new(new DateTime(EpochYear, 1, 1, 8, 0, 0, DateTimeKind.Unspecified));

    private static readonly DateTimeFormatInfo CalendarFormat = new()
    {
        DayNames =
        [
            "Emberday",
            "Ashday",
            "Ironday",
            "Ravenday",
            "Stormday",
            "Hollowday",
            "Duskday",
        ],
        MonthNames =
        [
            "Frostwane",
            "Coldmere",
            "Thawmoon",
            "Greentide",
            "Bloomrise",
            "Suncrest",
            "Highsun",
            "Emberfall",
            "Harvestide",
            "Russetmoon",
            "Graytide",
            "Hearthwane",
            "",
        ],
    };

    public static DateTime GetCurrentInGameDateTime(GameInstant gameTime) => gameTime.Value;

    public static string GetDayName(DayOfWeek day)
    {
        return CalendarFormat.DayNames[(int)day];
    }

    private static readonly Season[] MonthSeasons =
    [
        Season.Winter,
        Season.Winter,
        Season.Spring,
        Season.Spring,
        Season.Spring,
        Season.Summer,
        Season.Summer,
        Season.Summer,
        Season.Autumn,
        Season.Autumn,
        Season.Autumn,
        Season.Winter,
    ];

    public static Season GetCurrentSeason(GameInstant instant) =>
        MonthSeasons[instant.Value.Month - 1];

    public static InGameDate GetCurrentInGameDate(GameInstant instant)
    {
        var dateTime = instant.Value;
        return new InGameDate(
            dateTime.Year,
            dateTime.ToString("MMMM", CalendarFormat),
            dateTime.Day,
            dateTime.ToString("dddd", CalendarFormat),
            dateTime.DayOfWeek,
            dateTime.Hour
        );
    }
}
