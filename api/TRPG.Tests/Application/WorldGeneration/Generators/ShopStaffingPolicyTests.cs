using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class ShopStaffingPolicyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void Generate_NeverSchedulesMoreConcurrentWorkersThanWorkstations(int capacity)
    {
        // Act
        var schedule = ShopStaffingPolicy.Generate(BuildingType.Castle, capacity);

        // Assert
        AssertPeakCoverageDoesNotExceed(schedule, capacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void InnGenerate_NeverSchedulesMoreConcurrentWorkersThanWorkstations(int capacity)
    {
        // Act
        var schedule = InnStaffingPolicy.Generate(capacity);

        // Assert
        AssertPeakCoverageDoesNotExceed(schedule, capacity);
    }

    [Fact]
    public void StaffDayOffPatterns_AreAllPairwiseDisjoint()
    {
        // Arrange
        var patterns = ShopStaffingPolicy.StaffDayOffPatterns;

        // Act & Assert — this is what guarantees a shop is never fully unstaffed regardless of who's hired
        for (var i = 0; i < patterns.Length; i++)
        {
            for (var j = i + 1; j < patterns.Length; j++)
            {
                Assert.Empty(patterns[i].Intersect(patterns[j]));
            }
        }
    }

    [Fact]
    public void NonOverlappingDayOffPatterns_PartitionTheWeekWithNoOverlap()
    {
        // Act
        var patterns = StaffingPolicy.NonOverlappingDayOffPatterns;

        // Assert — the two positions never share a day off (so someone's always covering the single workstation),
        // and together their days off cover the whole week (each works exactly the days the other is off)
        Assert.Empty(patterns[0].Intersect(patterns[1]));
        Assert.Equal(7, patterns[0].Concat(patterns[1]).Distinct().Count());
    }

    private static void AssertPeakCoverageDoesNotExceed(StaffingSchedule schedule, int capacity)
    {
        var shifts = schedule.EmployeeShifts.Append(schedule.OwnerShift).OfType<Shift>();
        var peak = Enumerable
            .Range(0, 7 * 24)
            .Max(hour => shifts.Count(shift => WorksAt(shift, hour)));

        Assert.True(peak <= capacity);
    }

    private static bool WorksAt(Shift shift, int weekHour)
    {
        var day = (DayOfWeek)(weekHour / 24);
        var hour = weekHour % 24;
        if (shift.DaysOff.Contains(day))
        {
            return false;
        }

        return shift.WorkHours.Start < shift.WorkHours.End
            ? hour >= shift.WorkHours.Start && hour < shift.WorkHours.End
            : hour >= shift.WorkHours.Start || hour < shift.WorkHours.End;
    }
}
