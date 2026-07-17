using TRPG.Application.CreatureJobs;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.CreatureJobs;

public class CreatureJobSchedulingTests
{
    private readonly Guid _creatureId = Guid.NewGuid();

    [Theory]
    [InlineData(8, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(7, false)]
    public void IsActiveAtHour_HandlesNormalWindow(int hour, bool expected)
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 8, endHour: 20);

        // Act
        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, hour);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(22, true)]
    [InlineData(23, true)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(21, false)]
    public void IsActiveAtHour_HandlesMidnightWraparound(int hour, bool expected)
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 22, endHour: 6);

        // Act
        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, hour);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsFalse_WhenStartEqualsEnd()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(_creatureId, startHour: 0, endHour: 0);

        // Act
        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, 0);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsTrue_WhenSpecificDayMatchesCurrentWeekday()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(
            _creatureId,
            startHour: 8,
            endHour: 20,
            specificDay: DayOfWeek.Monday
        );

        // Act
        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Monday, 10);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsFalse_WhenSpecificDayDoesNotMatchCurrentWeekday()
    {
        // Arrange
        var job = Builders.MakeCreatureJob(
            _creatureId,
            startHour: 8,
            endHour: 20,
            specificDay: DayOfWeek.Monday
        );

        // Act
        var result = CreatureJobScheduling.IsActiveAtHour(job, DayOfWeek.Tuesday, 10);

        // Assert
        Assert.False(result);
    }
}
