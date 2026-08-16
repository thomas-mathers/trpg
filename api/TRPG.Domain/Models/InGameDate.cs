namespace TRPG.Domain.Models;

public record InGameDate(
    int Year,
    string MonthName,
    int Day,
    string WeekdayName,
    DayOfWeek Weekday,
    int Hour
);
