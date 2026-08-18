namespace TRPG.Domain.Models;

public class NpcConversationHistory
{
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NpcId { get; init; }
    public string Summary { get; set; } = "";
    public Guid WorldId { get; init; }
}

public class NpcConversation
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NpcConversationHistoryId { get; init; }
    public string Summary { get; init; } = "";
    public Guid WorldId { get; init; }
}
