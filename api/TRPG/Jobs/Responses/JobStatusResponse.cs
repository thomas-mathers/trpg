namespace TRPG.Jobs.Responses;

public enum JobStatus
{
    Queued,
    InProgress,
    Done,
    Failed,
}

public record JobStatusResponse(
    Guid JobId,
    JobStatus Status,
    string? ResultJson,
    string? ErrorMessage
);
