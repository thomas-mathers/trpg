using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Results;

namespace TRPG.Application.Quests.Queries;

public enum QuestDialogMode
{
    Offer,
    TurnIn,
}

public record QuestDialogResult(QuestConversationResult Quest, QuestDialogMode Mode);

public class GetQuestDialogForGiverQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid GiverId { get; init; }
    public required Guid QuestId { get; init; }
}

internal class GetQuestDialogForGiverQueryHandler(
    IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult> getQuestInteractions
) : IQueryHandler<GetQuestDialogForGiverQuery, QuestDialogResult?>
{
    public async Task<QuestDialogResult?> Handle(
        GetQuestDialogForGiverQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var interactions = await getQuestInteractions.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = query.GiverId,
                PlayerId = query.PlayerId,
                WorldId = query.WorldId,
            },
            cancellationToken
        );

        var readyQuest = interactions.ReadyToCompleteQuests.SingleOrDefault(quest =>
            quest.QuestId == query.QuestId
        );
        if (readyQuest != null)
        {
            return new QuestDialogResult(readyQuest, QuestDialogMode.TurnIn);
        }

        var availableQuest = interactions.AvailableQuests.SingleOrDefault(quest =>
            quest.QuestId == query.QuestId
        );
        return availableQuest is null
            ? null
            : new QuestDialogResult(availableQuest, QuestDialogMode.Offer);
    }
}
