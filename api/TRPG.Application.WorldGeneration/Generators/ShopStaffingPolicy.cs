using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal static class ShopStaffingPolicy
{
    internal static readonly IReadOnlyList<DayOfWeek>[] StaffDayOffPatterns =
    [
        [DayOfWeek.Saturday, DayOfWeek.Sunday],
        [DayOfWeek.Monday, DayOfWeek.Tuesday],
        [DayOfWeek.Wednesday, DayOfWeek.Thursday],
    ];

    internal static StaffingSchedule Generate(BuildingType type, int staffableWorkstationCount)
    {
        if (staffableWorkstationCount == 0)
        {
            return new StaffingSchedule(null, []);
        }

        var workHours = StaffingPolicy.GetWorkHoursForBuilding(type);
        var ownerProfession = StaffingPolicy.GetProfessionForBuilding(type);
        var employeeProfession = StaffingPolicy.GetEmployeeProfessionForBuilding(type);

        if (staffableWorkstationCount == 1)
        {
            return new StaffingSchedule(
                new Shift(
                    ownerProfession,
                    StaffingPolicy.NonOverlappingDayOffPatterns[0],
                    workHours
                ),
                [
                    new Shift(
                        employeeProfession,
                        StaffingPolicy.NonOverlappingDayOffPatterns[1],
                        workHours
                    ),
                ]
            );
        }

        var totalStaff = staffableWorkstationCount;
        var employeeShifts = new List<Shift>();
        for (var position = 1; position < totalStaff; position++)
        {
            employeeShifts.Add(
                new Shift(
                    employeeProfession,
                    StaffDayOffPatterns[position % StaffDayOffPatterns.Length],
                    workHours
                )
            );
        }

        return new StaffingSchedule(
            new Shift(ownerProfession, StaffDayOffPatterns[0], workHours),
            employeeShifts
        );
    }
}
