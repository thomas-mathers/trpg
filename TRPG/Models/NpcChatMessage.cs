namespace TRPG.Models;

public class NpcChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ConversationId { get; init; }
    public int Index { get; init; }
    public Guid SenderId { get; init; }
    public Guid RecipientId { get; init; }
    public string Message { get; init; } = "";
    public DateTime Date { get; init; }
    public NpcConversation Conversation { get; init; } = null!;
}
