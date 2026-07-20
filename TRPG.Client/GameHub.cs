using System.ComponentModel;
using Microsoft.AspNetCore.SignalR.Client;

namespace TRPG.Client;

public enum ConnectionStatus
{
    [Description("Connection lost, reconnecting...")]
    Reconnecting,

    [Description("Reconnected.")]
    Reconnected,

    [Description("Connection closed.")]
    Closed,
}

internal sealed class GameHub(HubConnection connection) : IAsyncDisposable
{
    public IAsyncEnumerable<string> StreamOpening(CancellationToken cancellationToken) =>
        connection.StreamAsync<string>("ReceiveOpening", cancellationToken);

    public IAsyncEnumerable<string> StreamChat(string input, CancellationToken cancellationToken) =>
        connection.StreamAsync<string>("SendChat", input, cancellationToken);

    public IAsyncEnumerable<string> StreamWait(int hours, CancellationToken cancellationToken) =>
        connection.StreamAsync<string>("SendWait", hours, cancellationToken);

    public void OnStatusChanged(Action<ConnectionStatus> onStatusChanged)
    {
        connection.Reconnecting += _ =>
        {
            onStatusChanged(ConnectionStatus.Reconnecting);
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            onStatusChanged(ConnectionStatus.Reconnected);
            return Task.CompletedTask;
        };
        connection.Closed += _ =>
        {
            onStatusChanged(ConnectionStatus.Closed);
            return Task.CompletedTask;
        };
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
