namespace TRPG.Domain.Models;

public class GameSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid PlayerId { get; init; }
    public TimeSpan Playtime { get; set; }
    public Guid? TrespassingBuildingId { get; set; }
}
