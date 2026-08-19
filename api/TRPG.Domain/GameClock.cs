using System.Globalization;
using TRPG.Domain.Models;

namespace TRPG.Domain;

public static class GameClock
{
    internal const int EpochYear = 975;
    private const double InGameHoursPerRealHour = 12.0;
    private static readonly DateTime WorldEpoch = new(EpochYear, 1, 1, 8, 0, 0);

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
        return WorldEpoch.AddHours(inGameHoursElapsed);
    }

    public static string GetDayName(DayOfWeek day)
    {
        return CalendarFormat.DayNames[(int)day];
    }

    public static InGameDate GetCurrentInGameDate(TimeSpan bankedPlaytime)
    {
        var dateTime = GetCurrentInGameDateTime(bankedPlaytime);
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
