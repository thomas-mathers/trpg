namespace TRPG.GameSessions.Hubs;

internal interface IGameClientCall
{
    Task Invoke(IGameClient client);
}

internal sealed class GameClientCall<TArguments>(
    TArguments arguments,
    Func<IGameClient, TArguments, Task> invoke
) : IGameClientCall
{
    public Task Invoke(IGameClient client) => invoke(client, arguments);
}

internal sealed class GameClientCall(Func<IGameClient, Task> invoke) : IGameClientCall
{
    public Task Invoke(IGameClient client) => invoke(client);
}
