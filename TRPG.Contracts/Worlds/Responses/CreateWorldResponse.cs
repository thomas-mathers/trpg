namespace TRPG.Contracts.Worlds.Responses;

public record CreateWorldResponse(Guid WorldId, Guid PlayerId, string WorldName);
