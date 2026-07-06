using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TRPG.Contracts;

namespace TRPG.Client;

internal sealed class GameServerClient(HttpClient httpClient) {
    private ClientWebSocket? _chatSocket;

    public SceneSnapshot? CurrentScene { get; private set; }

    public async Task<CreateWorldResponse> CreateWorld(CreateWorldRequest request,
        CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync("/worlds", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateWorldResponse>(cancellationToken);
        return result!;
    }

    public async Task<IReadOnlyList<WorldSummary>> ListWorlds(CancellationToken cancellationToken) {
        var result = await httpClient.GetFromJsonAsync<List<WorldSummary>>("/worlds", cancellationToken);
        return result ?? [];
    }

    public async Task DropWorld(Guid worldId, CancellationToken cancellationToken) {
        var response = await httpClient.DeleteAsync(new Uri($"/worlds/{worldId}", UriKind.Relative),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> StartSession(Guid worldId, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsync(new Uri($"/worlds/{worldId}/sessions", UriKind.Relative), null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(cancellationToken);

        await ConnectChatStream(result!.SessionId, cancellationToken);

        return result.SessionId;
    }

    private async Task ConnectChatStream(Guid sessionId, CancellationToken cancellationToken) {
        var socket = new ClientWebSocket();
        var scheme = httpClient.BaseAddress!.Scheme == "https" ? "wss" : "ws";
        var uri = new UriBuilder(httpClient.BaseAddress)
            { Scheme = scheme, Path = $"/sessions/{sessionId}/chat/stream" }.Uri;
        await socket.ConnectAsync(uri, cancellationToken);
        _chatSocket = socket;
    }

    public IAsyncEnumerable<string> ReceiveOpening(CancellationToken cancellationToken = default) {
        if (_chatSocket == null) {
            throw new InvalidOperationException("Chat stream is not connected — call StartSession first.");
        }

        return ReceiveTokensUntilDone(_chatSocket, cancellationToken);
    }

    public async IAsyncEnumerable<string> SendChat(string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (_chatSocket == null) {
            throw new InvalidOperationException("Chat stream is not connected — call StartSession first.");
        }

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes<ClientCommand>(new ChatCommand(message),
            JsonSerializerOptions.Web);
        await _chatSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationToken);

        await foreach (var token in ReceiveTokensUntilDone(_chatSocket, cancellationToken)) {
            yield return token;
        }
    }

    public async IAsyncEnumerable<string> SendWait(int hours,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        if (_chatSocket == null) {
            throw new InvalidOperationException("Chat stream is not connected — call StartSession first.");
        }

        var requestBytes = JsonSerializer.SerializeToUtf8Bytes<ClientCommand>(new WaitCommand(hours),
            JsonSerializerOptions.Web);
        await _chatSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationToken);

        await foreach (var token in ReceiveTokensUntilDone(_chatSocket, cancellationToken)) {
            yield return token;
        }
    }

    private async IAsyncEnumerable<string> ReceiveTokensUntilDone(WebSocket socket,
        [EnumeratorCancellation] CancellationToken cancellationToken) {
        while (true) {
            var streamMessage = await ReceiveStreamMessage(socket, cancellationToken);
            switch (streamMessage) {
                case ChatStreamDone:
                    yield break;
                case ChatStreamToken token:
                    yield return token.Text;
                    break;
                case ChatStreamScene scene:
                    CurrentScene = scene.Scene;
                    break;
            }
        }
    }

    private static async Task<ChatStreamMessage> ReceiveStreamMessage(WebSocket socket,
        CancellationToken cancellationToken) {
        using var stream = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            await stream.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken);
        } while (!result.EndOfMessage);

        stream.Position = 0;
        return (await JsonSerializer.DeserializeAsync<ChatStreamMessage>(stream, JsonSerializerOptions.Web,
            cancellationToken))!;
    }

    public async Task EndSession(CancellationToken cancellationToken) {
        if (_chatSocket == null) {
            return;
        }

        if (_chatSocket.State == WebSocketState.Open) {
            await _chatSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "session ended", cancellationToken);
        }

        _chatSocket.Dispose();
        _chatSocket = null;
    }
}
