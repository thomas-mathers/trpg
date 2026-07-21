using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using TRPG.Client.Extensions;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed record SessionConnection(GameHub Hub, Guid SessionId);

internal sealed class GameServerClient(HttpClient httpClient, ILoggerFactory loggerFactory)
{
    private readonly ILogger<GameServerClient> _logger = loggerFactory.CreateLogger<GameServerClient>();

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

    public async Task<SessionConnection> StartSession(
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

        return new SessionConnection(new GameHub(connection), result.SessionId);
    }

    public async Task<SceneSnapshot?> GetScene(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<SceneSnapshot>(
                new Uri($"/sessions/{sessionId}/scene", UriKind.Relative),
                TrpgJsonOptions.Default,
                cancellationToken
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[game] failed to fetch scene");
            AnsiConsole.AnnounceError($"[[Failed to fetch scene: {ex.Message.EscapeMarkup()}]]");
            return null;
        }
    }

    public async Task<IReadOnlyList<NamedEntity>> GetNamedEntities(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<NamedEntity>>(
                new Uri($"/sessions/{sessionId}/named-entities", UriKind.Relative),
                TrpgJsonOptions.Default,
                cancellationToken
            );
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[game] failed to fetch named entities");
            AnsiConsole.AnnounceError(
                $"[[Failed to fetch named entities: {ex.Message.EscapeMarkup()}]]"
            );
            return [];
        }
    }

    public async Task<IReadOnlyList<AbilitySummary>> GetAbilities(
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<AbilitySummary>>(
                new Uri($"/worlds/{worldId}/abilities", UriKind.Relative),
                TrpgJsonOptions.Default,
                cancellationToken
            );
            return result ?? [];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[game] failed to fetch abilities");
            AnsiConsole.AnnounceError($"[[Failed to fetch abilities: {ex.Message.EscapeMarkup()}]]");
            return [];
        }
    }

    public async Task<FightState?> GetFight(Guid worldId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(
                new Uri($"/worlds/{worldId}/fight", UriKind.Relative),
                cancellationToken
            );
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<FightState>(
                TrpgJsonOptions.Default,
                cancellationToken
            );
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[game] failed to fetch fight status");
            AnsiConsole.AnnounceError(
                $"[[Failed to fetch fight status: {ex.Message.EscapeMarkup()}]]"
            );
            return null;
        }
    }
}
