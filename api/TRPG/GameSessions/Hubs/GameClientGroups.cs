namespace TRPG.GameSessions.Hubs;

internal static class GameClientGroups
{
    public static string ForWorld(Guid worldId) => $"world:{worldId}";
}
