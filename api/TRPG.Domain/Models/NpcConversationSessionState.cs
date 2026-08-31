namespace TRPG.Domain.Models;

public class NpcConversationSessionState
{
    public Guid SessionId { get; init; }
    public Guid WorldId { get; init; }
    public Dictionary<string, Guid> OpenConversationCreatureIdsByName { get; init; } = [];
}
