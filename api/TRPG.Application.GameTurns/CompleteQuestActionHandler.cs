using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class CompleteQuestActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetQuestByIdQuery, Quest?> getQuestById,
    ICommandHandler<CompleteQuestCommand> completeQuest
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, questId, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid questId,
        CancellationToken cancellationToken
    )
    {
        var quest = await getQuestById.Handle(
            new GetQuestByIdQuery { Id = questId },
            cancellationToken
        );
        if (quest == null)
        {
            return ActionOutcome.Failed(ActionFailure.QuestUnavailable);
        }

        await completeQuest.Handle(
            new CompleteQuestCommand
            {
                PlayerId = session.PlayerId,
                QuestId = questId,
                WorldId = session.WorldId,
            },
            cancellationToken
        );

        return ActionOutcome.Success;
    }
}
