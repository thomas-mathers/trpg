namespace TRPG.Application.WorldSimulation.Events;

public interface ILocationScopedEvent
{
    Guid LocationId { get; }
}
