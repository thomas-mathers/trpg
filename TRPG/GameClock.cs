using System.Globalization;
using TRPG.Models;

namespace TRPG;

internal static class GameClock {
    internal const int EpochYear = 975;
    private const double InGameHoursPerRealHour = 20.0;
    private static readonly DateTime WorldEpoch = new(EpochYear, 1, 1, 8, 0, 0);

    private static readonly DateTimeFormatInfo CalendarFormat = new() {
        DayNames = ["Emberday", "Ashday", "Ironday", "Ravenday", "Stormday", "Hollowday", "Duskday"],
        MonthNames = [
            "Frostwane", "Coldmere", "Thawmoon", "Greentide", "Bloomrise", "Suncrest",
            "Highsun", "Emberfall", "Harvestide", "Russetmoon", "Graytide", "Hearthwane", ""
        ]
    };

    public static TimeSpan GetTotalPlaytime(GameSession session) {
        return session.BankedPlaytime + session.SessionStopwatch.Elapsed;
    }

    public static void AdvanceHours(GameSession session, int hours) {
        session.BankedPlaytime += TimeSpan.FromHours(hours / InGameHoursPerRealHour);
    }

    public static DateTime GetCurrentInGameDateTime(GameSession session) {
        var inGameHoursElapsed = GetTotalPlaytime(session).TotalHours * InGameHoursPerRealHour;
        return WorldEpoch.AddHours(inGameHoursElapsed);
    }

    public static string GetDayName(DayOfWeek day) {
        return CalendarFormat.DayNames[(int) day];
    }

    public static InGameDate GetCurrentInGameDate(GameSession session) {
        var dateTime = GetCurrentInGameDateTime(session);
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