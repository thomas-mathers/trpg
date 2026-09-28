using TRPG.Application.Common.Events;

namespace TRPG.Application.GameTurns.Events;

public enum CreatureMovementDirection
{
    Arrived,
    Departed,
}

public record CreaturesMovedEvent(
    CreatureMovementDirection Direction,
    IReadOnlyCollection<string> Names,
    string? PlaceName
) : GameClientEvent;
