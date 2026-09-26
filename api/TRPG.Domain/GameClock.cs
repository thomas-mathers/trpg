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
    private const double InGameHoursPerRealHour = 12.0;
    public static GameInstant Epoch { get; } =
        new(new DateTime(EpochYear, 1, 1, 8, 0, 0, DateTimeKind.Unspecified));

    public static TimeSpan RealTimePerInGameHour =>
        TimeSpan.FromHours(1.0 / InGameHoursPerRealHour);

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

    public static DateTime GetCurrentInGameDateTime(TimeSpan bankedPlaytime)
    {
        var inGameHoursElapsed = bankedPlaytime.TotalHours * InGameHoursPerRealHour;
        return (Epoch + TimeSpan.FromHours(inGameHoursElapsed)).Value;
    }

    public static GameInstant GetCurrentGameInstant(TimeSpan bankedPlaytime) =>
        new(GetCurrentInGameDateTime(bankedPlaytime));

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

    public static Season GetCurrentSeason(TimeSpan bankedPlaytime) =>
        GetCurrentSeason(GetCurrentGameInstant(bankedPlaytime));

    public static Season GetCurrentSeason(GameInstant instant) =>
        MonthSeasons[instant.Value.Month - 1];

    public static InGameDate GetCurrentInGameDate(TimeSpan bankedPlaytime) =>
        GetCurrentInGameDate(GetCurrentGameInstant(bankedPlaytime));

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
