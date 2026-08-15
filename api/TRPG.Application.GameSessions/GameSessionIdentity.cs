namespace TRPG.Application.GameSessions;

internal record GameSessionIdentity(Guid SessionId, Guid WorldId, Guid PlayerId);
