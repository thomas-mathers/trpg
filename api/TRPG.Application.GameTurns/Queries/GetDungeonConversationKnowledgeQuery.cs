using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public record GetDungeonConversationKnowledgeQuery(Guid WorldId, Guid PlayerId, Guid NpcId);

public record DungeonConversationKnowledge(
    string Purpose,
    string SharedHistory,
    string? DungeonHistory,
    string KnownRoute,
    string? LearnedAccount,
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
            "LearnedAccount is the only thing that establishes the survivor has learned what happened to their companion — it is set automatically once the player hands over the companion's journal, not by anything you narrate or any tool you call. Narrate the survivor's reaction only when LearnedAccount is present; never infer disclosure from the player merely possessing the journal, mentioning it, or starting a conversation. Accepting or turning in this or any other quest happens only through the player's own action outside of conversation — never narrate a quest as accepted, declined, completed, or rewarded, since that would not actually grant it and would wrongly persist as fact in this NPC's memory."
        );
    }
}
