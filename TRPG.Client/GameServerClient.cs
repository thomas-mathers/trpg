using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TRPG.Contracts;

namespace TRPG.Client;

internal sealed class GameServerClient(HttpClient httpClient)
{
    private HubConnection? _connection;
    private Guid? _sessionId;

    public SceneSnapshot? CurrentScene { get; private set; }

    public event Action<string>? ConnectionStatusChanged;

    public async Task<CreateWorldResponse> CreateWorld(
        CreateWorldRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await httpClient.PostAsJsonAsync("/worlds", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateWorldResponse>(
            cancellationToken
        );
        return result!;
    }

    public async Task<IReadOnlyList<WorldSummary>> ListWorlds(CancellationToken cancellationToken)
    {
        var result = await httpClient.GetFromJsonAsync<List<WorldSummary>>(
            "/worlds",
            cancellationToken
        );
        return result ?? [];
    }

    public async Task DropWorld(Guid worldId, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync(
            new Uri($"/worlds/{worldId}", UriKind.Relative),
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> StartSession(Guid worldId, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsync(
            new Uri($"/worlds/{worldId}/sessions", UriKind.Relative),
            null,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(
            cancellationToken
        );

        await ConnectChatStream(result!.SessionId, cancellationToken);

        return result.SessionId;
    }

    private async Task ConnectChatStream(Guid sessionId, CancellationToken cancellationToken)
    {
        var uri = new UriBuilder(httpClient.BaseAddress!)
        {
            Path = "/hubs/chat",
            Query = $"sessionId={sessionId}",
        }.Uri;

        var connection = new HubConnectionBuilder().WithUrl(uri).WithAutomaticReconnect().Build();
        connection.On<SceneSnapshot>("Scene", scene => CurrentScene = scene);
        connection.Reconnecting += _ =>
        {
            ConnectionStatusChanged?.Invoke("Connection lost, reconnecting...");
            return Task.CompletedTask;
        };
        connection.Reconnected += _ =>
        {
            ConnectionStatusChanged?.Invoke("Reconnected.");
            return Task.CompletedTask;
        };
        connection.Closed += _ =>
        {
            ConnectionStatusChanged?.Invoke("Connection closed.");
            return Task.CompletedTask;
        };

        await connection.StartAsync(cancellationToken);

        _connection = connection;
        _sessionId = sessionId;
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken = default)
    {
        if (_connection == null)
        {
            throw new InvalidOperationException(
                "Chat stream is not connected — call StartSession first."
            );
        }

        return _connection.StreamAsync<string>("ReceiveOpening", cancellationToken);
    }

    public IAsyncEnumerable<string> SendChat(
        string message,
        CancellationToken cancellationToken = default
    )
    {
        if (_connection == null)
        {
            throw new InvalidOperationException(
                "Chat stream is not connected — call StartSession first."
            );
        }

        return _connection.StreamAsync<string>("SendChat", message, cancellationToken);
    }

    public IAsyncEnumerable<string> SendWait(
        int hours,
        CancellationToken cancellationToken = default
    )
    {
        if (_connection == null)
        {
            throw new InvalidOperationException(
                "Chat stream is not connected — call StartSession first."
            );
        }

        return _connection.StreamAsync<string>("SendWait", hours, cancellationToken);
    }

    public async Task EndSession(CancellationToken cancellationToken)
    {
        if (_sessionId == null)
        {
            return;
        }

        var response = await httpClient.DeleteAsync(
            new Uri($"/sessions/{_sessionId}", UriKind.Relative),
            cancellationToken
        );
        response.EnsureSuccessStatusCode();

        if (_connection != null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }

        _sessionId = null;
    }
}
