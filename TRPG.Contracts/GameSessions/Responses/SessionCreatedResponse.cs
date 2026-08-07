namespace TRPG.Contracts.GameSessions.Responses;

public record SessionCreatedResponse(Guid SessionId, Guid PlayerId);
