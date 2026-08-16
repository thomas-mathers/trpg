namespace TRPG.Domain.Models;

public class NpcConversation
{
    public Guid CreatureId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NpcId { get; init; }
    public string Summary { get; set; } = "";
    public Guid WorldId { get; init; }
}
