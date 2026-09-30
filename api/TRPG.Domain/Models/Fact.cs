namespace TRPG.Domain.Models;

// A fact remains the same knowledge regardless of which conversation or artifact reveals it.
public class Fact
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public string Subject { get; init; } = "";
    public string Value { get; init; } = "";
}
