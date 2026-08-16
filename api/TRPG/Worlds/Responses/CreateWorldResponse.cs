namespace TRPG.Worlds.Responses;

public record CreateWorldResponse(Guid WorldId, Guid PlayerId, string WorldName);
