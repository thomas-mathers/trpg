namespace TRPG.Models;

internal class NpcConversation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid NpcId { get; init; }
    public string Summary { get; set; } = "";
    public int? LastSummarizedIndex { get; set; }
}
