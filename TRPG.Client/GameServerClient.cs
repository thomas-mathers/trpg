using System.Net.Http.Json;
using TRPG.Contracts;

namespace TRPG.Client;

internal sealed class GameServerClient(HttpClient httpClient) {
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

    public async Task<CreateSessionResponse> StartSession(Guid worldId, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsync(new Uri($"/worlds/{worldId}/sessions", UriKind.Relative), null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(cancellationToken);
        return result!;
    }

    public async Task<ChatResponse> SendChat(Guid sessionId, string message, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync($"/sessions/{sessionId}/chat", new ChatRequest(message),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        return result!;
    }

    public async Task<WaitResponse> Wait(Guid sessionId, int hours, CancellationToken cancellationToken) {
        var response = await httpClient.PostAsJsonAsync($"/sessions/{sessionId}/wait", new WaitRequest(hours),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WaitResponse>(cancellationToken);
        return result!;
    }

    public async Task EndSession(Guid sessionId, CancellationToken cancellationToken) {
        var response = await httpClient.DeleteAsync(new Uri($"/sessions/{sessionId}", UriKind.Relative),
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
