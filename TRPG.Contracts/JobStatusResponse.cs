namespace TRPG.Contracts;

public record JobStatusResponse(Guid JobId, JobStatus Status, string? ResultJson, string? ErrorMessage);
