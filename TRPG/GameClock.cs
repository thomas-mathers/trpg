using System.Globalization;

namespace TRPG;

internal record InGameDate(int Year, string MonthName, int Day, string WeekdayName, int Hour);

internal static class GameClock {
    public const double InGameHoursPerRealHour = 20.0;
    public static readonly DateTime WorldEpoch = new(975, 1, 1);

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

    public static InGameDate GetCurrentInGameDate(GameSession session) {
        var dateTime = GetCurrentInGameDateTime(session);
        return new InGameDate(
            dateTime.Year,
            dateTime.ToString("MMMM", CalendarFormat),
            dateTime.Day,
            dateTime.ToString("dddd", CalendarFormat),
            dateTime.Hour
        );
    }
}