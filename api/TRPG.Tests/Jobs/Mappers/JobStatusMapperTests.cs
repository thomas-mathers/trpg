using TickerQ.Utilities.Enums;
using TRPG.Jobs.Mappers;
using TRPG.Jobs.Responses;

namespace TRPG.Tests.Jobs.Mappers;

public sealed class JobStatusMapperTests
{
    [Theory]
    [InlineData(TickerStatus.Idle, JobStatus.Idle)]
    [InlineData(TickerStatus.Queued, JobStatus.Queued)]
    [InlineData(TickerStatus.InProgress, JobStatus.InProgress)]
    [InlineData(TickerStatus.Done, JobStatus.Done)]
    [InlineData(TickerStatus.Failed, JobStatus.Failed)]
    [InlineData(TickerStatus.Cancelled, JobStatus.Cancelled)]
    public void ToJobStatus_MapsTheMatchingStatus(TickerStatus status, JobStatus expected)
    {
        // Act
        var result = status.ToJobStatus();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ToJobStatus_TreatsAJobThatFinishedAfterItsDueTime_AsDone()
    {
        // Act
        var result = TickerStatus.DueDone.ToJobStatus();

        // Assert
        Assert.Equal(JobStatus.Done, result);
    }

    [Fact]
    public void ToJobStatus_TreatsASkippedJob_AsCancelled()
    {
        // Act
        var result = TickerStatus.Skipped.ToJobStatus();

        // Assert
        Assert.Equal(JobStatus.Cancelled, result);
    }
}
