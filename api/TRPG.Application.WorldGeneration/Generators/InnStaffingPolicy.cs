using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class InnStaffingPolicy
{
    internal static StaffingSchedule Generate(int staffableWorkstationCount)
    {
        if (staffableWorkstationCount == 0)
        {
            return new StaffingSchedule(null, []);
        }

        var dayShiftHours = new HourWindow(6, 18);
        var nightShiftHours = new HourWindow(18, 6);

        var employees = new List<Shift>
        {
            new(
                Profession.Innkeeper,
                StaffingPolicy.NonOverlappingDayOffPatterns[1],
                dayShiftHours
            ),
            new(
                Profession.Innkeeper,
                StaffingPolicy.NonOverlappingDayOffPatterns[0],
                nightShiftHours
            ),
            new(
                Profession.Innkeeper,
                StaffingPolicy.NonOverlappingDayOffPatterns[1],
                nightShiftHours
            ),
        };
        for (var position = 1; position < staffableWorkstationCount; position++)
        {
            employees.Add(new Shift(Profession.Innkeeper, [], dayShiftHours));
            employees.Add(new Shift(Profession.Innkeeper, [], nightShiftHours));
        }

        return new StaffingSchedule(
            new Shift(
                Profession.Innkeeper,
                StaffingPolicy.NonOverlappingDayOffPatterns[0],
                dayShiftHours
            ),
            employees
        );
    }
}
