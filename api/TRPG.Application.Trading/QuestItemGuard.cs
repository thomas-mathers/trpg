using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Queries;

namespace TRPG.Application.Trading;

internal class QuestItemGuard(
    IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>> getActiveQuestItemIds
)
{
    public async Task EnsureNotActiveQuestItems(
        Guid playerId,
        IReadOnlyCollection<Guid> itemIds,
        CancellationToken cancellationToken
    )
    {
        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = playerId },
            cancellationToken
        );
        var questItemId = itemIds.FirstOrDefault(questItemIds.Contains);

        if (questItemId != Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Item {questItemId} is required for an active quest and cannot be removed from your inventory."
            );
        }
    }
}
