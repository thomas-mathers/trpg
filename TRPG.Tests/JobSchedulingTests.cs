using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

public class JobSchedulingTests {
    [Theory]
    [InlineData(8, true)]
    [InlineData(19, true)]
    [InlineData(20, false)]
    [InlineData(7, false)]
    public void IsActiveAtHour_HandlesNormalWindow(int hour, bool expected) {
        // Arrange
        var job = Builders.MakeJob(Guid.NewGuid(), startHour: 8, endHour: 20);

        // Act
        var result = JobScheduling.IsActiveAtHour(job, hour);

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
    public void IsActiveAtHour_HandlesMidnightWraparound(int hour, bool expected) {
        // Arrange
        var job = Builders.MakeJob(Guid.NewGuid(), startHour: 22, endHour: 6);

        // Act
        var result = JobScheduling.IsActiveAtHour(job, hour);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsActiveAtHour_ReturnsFalse_WhenStartEqualsEnd() {
        // Arrange
        var job = Builders.MakeJob(Guid.NewGuid(), startHour: 0, endHour: 0);

        // Act
        var result = JobScheduling.IsActiveAtHour(job, 0);

        // Assert
        Assert.False(result);
    }
}