namespace TRPG.Domain.Models;

public class ChatMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public int Ordinal { get; init; }
    public string Role { get; init; } = "";
    public string MessageJson { get; init; } = "";
}
