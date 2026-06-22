namespace TRPG.Models;

internal class NpcChatMessage {
    public NpcConversation Conversation { get; init; } = null!;
    public Guid ConversationId { get; init; }
    public DateTime Date { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public int Index { get; init; }
    public string Message { get; init; } = "";
    public Guid RecipientId { get; init; }
    public Guid SenderId { get; init; }
}