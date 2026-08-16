namespace TRPG.Application.GameTurns;

public record GameTurnSession(Guid SessionId, Guid WorldId, Guid PlayerId);
