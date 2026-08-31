using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class InnStaffingPolicy
{
    internal static StaffingSchedule Generate()
    {
        var dayShiftHours = new HourWindow(6, 18);
        var nightShiftHours = new HourWindow(18, 6);

        return new StaffingSchedule(
            new Shift(
                Profession.Innkeeper,
                StaffingPolicy.NonOverlappingDayOffPatterns[0],
                dayShiftHours
            ),
            [
                new Shift(
                    Profession.Innkeeper,
                    StaffingPolicy.NonOverlappingDayOffPatterns[1],
                    dayShiftHours
                ),
                new Shift(
                    Profession.Innkeeper,
                    StaffingPolicy.NonOverlappingDayOffPatterns[0],
                    nightShiftHours
                ),
                new Shift(
                    Profession.Innkeeper,
                    StaffingPolicy.NonOverlappingDayOffPatterns[1],
                    nightShiftHours
                ),
            ]
        );
    }
}
