using TRPG.Application.Common.Queries;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.GameTurns.Queries;

public record GetOpenDungeonConversationKnowledgeQuery(Guid WorldId, Guid SessionId, Guid PlayerId);

internal class GetOpenDungeonConversationKnowledgeQueryHandler(
    IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>> getConversations,
    IQueryHandler<GetDungeonExpeditionSurvivorIdsQuery, IReadOnlySet<Guid>> getSurvivorIds,
    IQueryHandler<GetDungeonConversationKnowledgeQuery, DungeonConversationKnowledge?> getKnowledge
)
    : IQueryHandler<
        GetOpenDungeonConversationKnowledgeQuery,
        IReadOnlyList<DungeonConversationKnowledge>
    >
{
    public async Task<IReadOnlyList<DungeonConversationKnowledge>> Handle(
        GetOpenDungeonConversationKnowledgeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var conversations = await getConversations.Handle(
            new GetOpenNpcConversationsQuery { SessionId = query.SessionId },
            cancellationToken
        );
        if (conversations.Count == 0)
        {
            return [];
        }

        // Cheap existence check first, so an ordinary conversation with a non-survivor NPC never
        // pays for the full expedition/secret/location lookup below.
        var survivorIds = await getSurvivorIds.Handle(
            new GetDungeonExpeditionSurvivorIdsQuery(query.WorldId, conversations.Values.ToArray()),
            cancellationToken
        );
        if (survivorIds.Count == 0)
        {
            return [];
        }

        var result = new List<DungeonConversationKnowledge>();
        foreach (var npcId in conversations.Values.Where(survivorIds.Contains))
        {
            var knowledge = await getKnowledge.Handle(
                new GetDungeonConversationKnowledgeQuery(
                    WorldId: query.WorldId,
                    PlayerId: query.PlayerId,
                    NpcId: npcId
                ),
                cancellationToken
            );
            if (knowledge != null)
                result.Add(knowledge);
        }
        return result.ToArray();
    }
}
