namespace TRPG.Application.GameSessions.Exceptions;

public class GameSessionNotFoundException(Guid sessionId)
    : Exception($"Game session {sessionId} not found.");
