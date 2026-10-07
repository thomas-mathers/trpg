using TickerQ.Utilities.Enums;
using TRPG.Jobs.Responses;

namespace TRPG.Jobs.Mappers;

internal static class JobStatusMapper
{
    public static JobStatus ToJobStatus(this TickerStatus status) =>
        status switch
        {
            TickerStatus.Idle => JobStatus.Idle,
            TickerStatus.Queued => JobStatus.Queued,
            TickerStatus.InProgress => JobStatus.InProgress,
            TickerStatus.Done or TickerStatus.DueDone => JobStatus.Done,
            TickerStatus.Failed => JobStatus.Failed,
            TickerStatus.Cancelled or TickerStatus.Skipped => JobStatus.Cancelled,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };
}
