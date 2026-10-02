using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Quests.Queries;

namespace TRPG.Application.GameTurns;

internal class ActivateTriggerActionHandler(
    GameActionRunner actionRunner,
    IQueryHandler<GetHiddenQuestTriggerIdsQuery, IReadOnlySet<Guid>> getHiddenQuestTriggerIds,
    ICommandHandler<ActivateTriggerCommand, ActivateTriggerResult> activateTrigger
)
{
    public Task<ActionOutcome> Handle(
        GameTurnSession session,
        Guid triggerId,
        CancellationToken cancellationToken = default
    ) => actionRunner.Run(session, ct => Resolve(session, triggerId, ct), cancellationToken);

    private async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid triggerId,
        CancellationToken cancellationToken
    )
    {
        var hiddenTriggerIds = await getHiddenQuestTriggerIds.Handle(
            new GetHiddenQuestTriggerIdsQuery
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                TriggerIds = [triggerId],
            },
            cancellationToken
        );
        if (hiddenTriggerIds.Contains(triggerId))
        {
            return ActionOutcome.Failed(ActionFailure.NothingToActivate);
        }

        var result = await activateTrigger.Handle(
            new ActivateTriggerCommand { TriggerId = triggerId, PlayerId = session.PlayerId },
            cancellationToken
        );

        if (result.AlreadyActivated)
        {
            return ActionOutcome.Failed(ActionFailure.AlreadyActivated);
        }
        return ActionOutcome.Success;
    }
}
