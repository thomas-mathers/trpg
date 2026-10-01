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
    public async Task<GameTurnPrompt> Resolve(
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
                return new GameTurnPrompt.Reply(BuildRejectionMessage(rejected.Outcome));
            case MoveTravelDeathResult:
                return new GameTurnPrompt.Narrate(
                    "The player died from a lingering effect during the journey and never arrived. Narrate their death in two or three sentences.",
                    IncludeTools: false
                );
            case MoveInterruptedResult:
                return new GameTurnPrompt.None();
            case MoveCompletedResult completed:
                turnContext.PlayerMoved = true;
                await RecordArrival(session, completed, cancellationToken);
                return new GameTurnPrompt.None();
            default:
                throw new ArgumentOutOfRangeException(nameof(result));
        }
    }

    internal static string BuildRejectionMessage(EntryOutcome outcome) =>
        outcome switch
        {
            EntryOutcome.NoEntrance => "There is no way in.",
            EntryOutcome.Locked => "The door is locked.",
            EntryOutcome.DestinationNotFound or EntryOutcome.ExitNotFound =>
                "There is nothing to enter there.",
            EntryOutcome.EncounterActive =>
                "A hostile encounter is already underway. Resolve it before moving.",
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
