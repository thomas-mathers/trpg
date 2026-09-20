namespace TRPG.Domain.Models;

public class FactionStanding
{
    public Guid FactionId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid OtherFactionId { get; init; }
    public int Score { get; set; }
    public Guid WorldId { get; init; }
}
