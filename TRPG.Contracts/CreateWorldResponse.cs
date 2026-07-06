namespace TRPG.Contracts;

public record CreateWorldResponse(Guid WorldId, Guid PlayerId, string WorldName);