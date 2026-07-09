namespace TRPG.Data.Models;

public record InGameDate(
    int Year,
    string MonthName,
    int Day,
    string WeekdayName,
    DayOfWeek Weekday,
    int Hour
);
