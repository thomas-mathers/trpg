using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public record GetDungeonConversationKnowledgeQuery(Guid WorldId, Guid PlayerId, Guid NpcId);

public record ShareableDungeonDiscovery(Guid ExpeditionId, string Fact);

public record DungeonConversationKnowledge(
    string Purpose,
    string SharedHistory,
    string? DungeonHistory,
    string KnownRoute,
    string? LearnedAccount,
    ShareableDungeonDiscovery? PlayerCanShare,
    string Guidance
);

internal class GetDungeonConversationKnowledgeQueryHandler(
    IQueryHandler<GetDungeonExpeditionsQuery, IReadOnlyList<DungeonExpedition>> getExpeditions,
    IQueryHandler<GetKnownSecretIdsQuery, IReadOnlyList<Guid>> getKnownSecrets,
    IQueryHandler<GetBuildingByIdQuery, Building?> getBuilding,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocations
) : IQueryHandler<GetDungeonConversationKnowledgeQuery, DungeonConversationKnowledge?>
{
    public async Task<DungeonConversationKnowledge?> Handle(
        GetDungeonConversationKnowledgeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var expeditions = await getExpeditions.Handle(
            new GetDungeonExpeditionsQuery(query.WorldId),
            cancellationToken
        );
        var expedition = expeditions.SingleOrDefault(expedition =>
            expedition.SurvivorId == query.NpcId
        );
        if (expedition == null)
            return null;

        var npcSecrets = await getKnownSecrets.Handle(
            new GetKnownSecretIdsQuery(WorldId: query.WorldId, KnowerId: query.NpcId),
            cancellationToken
        );
        var playerSecrets = await getKnownSecrets.Handle(
            new GetKnownSecretIdsQuery(WorldId: query.WorldId, KnowerId: query.PlayerId),
            cancellationToken
        );
        var locations = await getLocations.Handle(
            new GetLocationsByIdsQuery { Ids = expedition.KnownRouteLocationIds },
            cancellationToken
        );
        var routeNames = expedition
            .KnownRouteLocationIds.Where(locations.ContainsKey)
            .Select(id => locations[id].Name)
            .ToArray();

        var building = await getBuilding.Handle(
            new GetBuildingByIdQuery { Id = expedition.BuildingId },
            cancellationToken
        );
        return new DungeonConversationKnowledge(
            expedition.Purpose,
            expedition.Separation,
            building?.Premise,
            $"Shared route, in order: {string.Join(" → ", routeNames)}. Knows the return route outside; has no live awareness of these rooms or knowledge beyond the separation point.",
            npcSecrets.Contains(expedition.DiscoverySecretId) ? expedition.Discovery : null,
            playerSecrets.Contains(expedition.DiscoverySecretId)
                ? new ShareableDungeonDiscovery(expedition.Id, expedition.Discovery)
                : null,
            "PlayerCanShare is private player knowledge, not NPC knowledge. Only LearnedAccount establishes that the survivor received the news. When the player explicitly shares it, call share_expedition_discovery before narrating the reaction. Never infer disclosure from possession, earlier conversation summaries, or starting a conversation. A failed tool call does not establish disclosure. Do not promise quests or rewards."
        );
    }
}
