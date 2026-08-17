using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventBuffer eventBuffer,
    IHubContext<ChatHub, IGameClient> hubContext,
    IEnumerable<IGameClientEventMapper> eventMappers
) : IGameClientEventDispatcher
{
    private readonly IReadOnlyDictionary<Type, IGameClientEventMapper> _eventMappers =
        eventMappers.ToDictionary(mapper => mapper.EventType);

    public async Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        var client = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));
        foreach (var gameEvent in eventBuffer.Drain())
        {
            var mapper =
                _eventMappers.GetValueOrDefault(gameEvent.GetType())
                ?? throw new InvalidOperationException(
                    $"No client event mapper is registered for {gameEvent.GetType().Name}."
                );
            await mapper.Map(gameEvent).Invoke(client);
        }
    }
}
