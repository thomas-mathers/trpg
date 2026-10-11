namespace TRPG.Application.Scenes.Events;

public interface ILocationScopedEvent
{
    Guid LocationId { get; }
}
