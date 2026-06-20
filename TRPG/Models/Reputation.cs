namespace TRPG.Models;

public class Reputation
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PersonId { get; init; }
    public Guid FactionId { get; init; }
    public int Score { get; set; }
}
