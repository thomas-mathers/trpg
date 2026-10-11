using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Events;

namespace TRPG.Tests.Helpers;

internal sealed class RecordingGameClientEventSink : IGameClientEventSink
{
    private readonly Lock _lock = new();
    private readonly List<GameClientEvent> _events = [];

    public IReadOnlyList<GameClientEvent> Events
    {
        get
        {
            lock (_lock)
            {
                return _events.ToArray();
            }
        }
    }

    public void Enqueue(GameClientEvent gameEvent)
    {
        lock (_lock)
        {
            _events.Add(gameEvent);
        }
    }
}

internal static class RecordingGameClientEventSinkExtensions
{
    // Runner-created scopes cannot see the scoped test sink, so events are recorded in a singleton.
    public static IServiceCollection AddRecordingGameClientEvents(
        this IServiceCollection services
    ) =>
        services
            .AddSingleton<RecordingGameClientEventSink>()
            .AddSingleton<IGameClientEventSink>(provider =>
                provider.GetRequiredService<RecordingGameClientEventSink>()
            );
}
