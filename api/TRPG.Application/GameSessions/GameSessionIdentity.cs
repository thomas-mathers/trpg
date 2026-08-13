namespace TRPG.Application.GameSessions;

public record GameSessionIdentity(Guid SessionId, Guid WorldId, Guid PlayerId);
