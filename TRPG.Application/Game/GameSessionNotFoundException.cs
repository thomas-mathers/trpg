namespace TRPG.Application.Game;

public class GameSessionNotFoundException(Guid sessionId)
    : Exception($"Game session {sessionId} not found.");
