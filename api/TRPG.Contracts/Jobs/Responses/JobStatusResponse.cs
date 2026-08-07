namespace TRPG.Contracts.Jobs.Responses;

public enum JobStatus
{
    Idle,
    Queued,
    InProgress,
    Done,
    Failed,
    Cancelled,
}

public record JobStatusResponse(
    Guid JobId,
    JobStatus Status,
    string? ResultJson,
    string? ErrorMessage
);
