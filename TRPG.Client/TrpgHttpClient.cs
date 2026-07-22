using System.Net;
using System.Net.Http.Json;
using TRPG.Contracts;
using TRPG.Contracts.Abilities.Responses;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Creatures.Requests;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Contracts.GameSessions.Responses;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Contracts.Jobs.Responses;
using TRPG.Contracts.Scenes.Responses;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Contracts.Worlds.Responses;

namespace TRPG.Client;

internal sealed class TrpgHttpClient(HttpClient httpClient)
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

    public async Task<CreateSessionResponse> StartSession(
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
        return result!;
    }

    public async Task<SceneSnapshot> GetScene(Guid sessionId, CancellationToken cancellationToken)
    {
        var result = await httpClient.GetFromJsonAsync<SceneSnapshot>(
            new Uri($"/sessions/{sessionId}/scene", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!;
    }

    public async Task<IReadOnlyList<NamedEntity>> GetNamedEntities(
        Guid sessionId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<List<NamedEntity>>(
            new Uri($"/sessions/{sessionId}/named-entities", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result ?? [];
    }

    public async Task<IReadOnlyList<AbilitySummary>> GetAbilities(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<List<AbilitySummary>>(
            new Uri($"/creatures/{creatureId}/abilities", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result ?? [];
    }

    public async Task<IReadOnlyList<UsableItemSummary>> GetUsableItems(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<List<UsableItemSummary>>(
            new Uri($"/creatures/{creatureId}/items", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result ?? [];
    }

    public async Task<int> GetUnallocatedAttributePoints(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<AttributePointsResponse>(
            new Uri($"/creatures/{creatureId}/attribute-points", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!.UnallocatedPoints;
    }

    public async Task<int> GetLevel(Guid creatureId, CancellationToken cancellationToken)
    {
        var result = await httpClient.GetFromJsonAsync<CreatureLevelResponse>(
            new Uri($"/creatures/{creatureId}/level", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!.Level;
    }

    public async Task<CreatureGenerationOptionsResponse> GetCreatureGenerationOptions(
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<CreatureGenerationOptionsResponse>(
            new Uri("/creature-generation/options", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!;
    }

    public async Task AllocateAttributePoints(
        Guid creatureId,
        IReadOnlyDictionary<AttributeName, int> deltas,
        CancellationToken cancellationToken
    )
    {
        var response = await httpClient.PostAsJsonAsync(
            new Uri($"/creatures/{creatureId}/attribute-points/allocate", UriKind.Relative),
            new AllocateAttributePointsRequest(deltas),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<SkillProgressSummary>> GetSkills(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<List<SkillProgressSummary>>(
            new Uri($"/creatures/{creatureId}/skills", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result ?? [];
    }

    public async Task<BaseAttributesResponse> GetBaseAttributes(
        Guid creatureId,
        CancellationToken cancellationToken
    )
    {
        var result = await httpClient.GetFromJsonAsync<BaseAttributesResponse>(
            new Uri($"/creatures/{creatureId}/attributes", UriKind.Relative),
            TrpgJsonOptions.Default,
            cancellationToken
        );
        return result!;
    }

    public async Task<FightState?> GetFight(Guid playerId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(
            new Uri($"/players/{playerId}/fight", UriKind.Relative),
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
}
