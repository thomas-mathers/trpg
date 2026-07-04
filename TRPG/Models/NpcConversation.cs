namespace TRPG.Models;

internal class NpcConversation {
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid NpcId { get; init; }
    public Guid PersonId { get; init; }
    public string Summary { get; set; } = "";
    public Guid WorldId { get; init; }
}