namespace TRPG.Models;

internal class Reputation {
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public int Score { get; set; }
}