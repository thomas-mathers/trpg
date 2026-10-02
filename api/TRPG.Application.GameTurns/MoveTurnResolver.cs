using Microsoft.Extensions.AI;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;

namespace TRPG.Application.GameTurns;

internal class MoveTurnResolver(
    GameTurnContext turnContext,
    ICommandHandler<ExecutePlayerMoveCommand, ExecutePlayerMoveResult> executePlayerMove,
    ICommandHandler<AppendChatMessagesCommand, int> appendChatMessages
)
{
    public async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid connectorId,
        CancellationToken cancellationToken = default
    )
    {
        var result = await executePlayerMove.Handle(
            new ExecutePlayerMoveCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                ConnectorId = connectorId,
            },
            cancellationToken
        );

        switch (result)
        {
            case MoveRejectedResult rejected:
                return ActionOutcome.Failed(ToFailure(rejected.Outcome));
            case MoveInterruptedResult:
                return ActionOutcome.Success;
            case MoveCompletedResult completed:
                turnContext.PlayerMoved = true;
                await RecordArrival(session, completed, cancellationToken);
                return ActionOutcome.Success;
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    internal static ActionFailure ToFailure(EntryOutcome outcome) =>
        outcome switch
        {
            EntryOutcome.NoEntrance => ActionFailure.NoEntrance,
            EntryOutcome.Locked => ActionFailure.Locked,
            EntryOutcome.DestinationNotFound or EntryOutcome.ExitNotFound =>
                ActionFailure.NothingToEnter,
            EntryOutcome.EncounterActive => ActionFailure.EncounterActive,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

    private async Task RecordArrival(
        GameTurnSession session,
        MoveCompletedResult completed,
        CancellationToken cancellationToken
    ) =>
        await appendChatMessages.Handle(
            new AppendChatMessagesCommand
            {
                SessionId = session.SessionId,
                Messages =
                [
                    new ChatMessage(
                        ChatRole.User,
                        RelocationFacts.DescribeArrival(completed.Scene)
                    ),
                ],
            },
            cancellationToken
        );
}
