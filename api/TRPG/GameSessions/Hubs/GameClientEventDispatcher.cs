using Microsoft.AspNetCore.SignalR;
using TRPG.Application.Common.Events;

namespace TRPG.GameSessions.Hubs;

internal sealed class GameClientEventDispatcher(
    IGameClientEventBuffer eventBuffer,
    IHubContext<ChatHub> hubContext,
    IEnumerable<IGameClientEventFormatter> eventFormatters
) : IGameClientEventDispatcher
{
    private readonly IReadOnlyDictionary<Type, IGameClientEventFormatter> _eventFormatters =
        eventFormatters.ToDictionary(formatter => formatter.EventType);

    public async Task FlushAsync(Guid worldId, CancellationToken cancellationToken = default)
    {
        var clients = hubContext.Clients.Group(GameClientGroups.ForWorld(worldId));
        foreach (var gameEvent in eventBuffer.Drain())
        {
            var message = _eventFormatters.TryGetValue(gameEvent.GetType(), out var formatter)
                ? formatter.Format(gameEvent)
                : new GameClientMessage(gameEvent.MethodName, gameEvent.Payload);

            if (message.Payload == null)
            {
                await clients.SendAsync(message.MethodName, cancellationToken);
            }
            else
            {
                await clients.SendAsync(message.MethodName, message.Payload, cancellationToken);
            }
        }
    }
}
