namespace TRPG.Application.WorldSimulation;

public sealed class WorldUnavailableException(Guid worldId, Exception? innerException = null)
    : Exception($"The simulation of world {worldId} is unavailable.", innerException);
