namespace TRPG.Domain.Models;

// A door gated by several levers gets one row per lever — the AND-gate this represents unlocks
// only once every row's Lever.IsPulled is true, generalizing from one lever to any number.
public class DoorConnectorLever
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid LeverId { get; init; }
    public Guid DoorConnectorId { get; init; }
    public Guid WorldId { get; init; }
}
