using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TRPG.Contracts;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class GameServerClient(HttpClient httpClient, ILoggerFactory loggerFactory)
{
    public async Task<Guid> CreateWorld(
        CreateWorldRequest request,
        CancellationToken cancellationToken
    )
    {
        var response = await httpClient.PostAsJsonAsync(
            "/worlds",
            request,
            TrpgJsonOptions.Default,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<EnqueueJobResponse>(
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!.JobId;
    }

    public async Task<JobStatusResponse> GetJobStatus(
        Guid jobId,
        CancellationToken cancellationToken
    )
    {
        var response = await httpClient.GetAsync(
            new Uri($"/jobs/{jobId}", UriKind.Relative),
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JobStatusResponse>(
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!;
    }

    public async Task<IReadOnlyList<WorldSummary>> ListWorlds(CancellationToken cancellationToken)
    {
        var result = await httpClient.GetFromJsonAsync<List<WorldSummary>>(
            "/worlds",
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result?.OrderBy(c => c.Name).ToArray() ?? [];
    }

    public async Task DropWorld(Guid worldId, CancellationToken cancellationToken)
    {
        var response = await httpClient.DeleteAsync(
            new Uri($"/worlds/{worldId}", UriKind.Relative),
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task<HubConnection> StartSession(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var response = await httpClient.PostAsync(
            new Uri($"/sessions?worldId={worldId}", UriKind.Relative),
            null,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(
            TrpgJsonOptions.Default,
            cancellationToken
        );

        var uri = new UriBuilder(httpClient.BaseAddress!)
        {
            Path = "/hubs/chat",
            Query = $"sessionId={result!.SessionId}",
        }.Uri;

        var builder = new HubConnectionBuilder().WithUrl(uri).WithAutomaticReconnect();
        builder.Services.AddSingleton(loggerFactory);
        var connection = builder.Build();
        await connection.StartAsync(cancellationToken);

        return connection;
    }
}
