using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;

namespace TRPG.Application.GameTurns;

internal class StreamStandUpTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<StandUpCommand, StandUpResult> standUp
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            async ct =>
            {
                var result = await standUp.Handle(
                    new StandUpCommand { PlayerId = session.PlayerId },
                    ct
                );
                return result switch
                {
                    StandUpResult.Success => new GameTurnPrompt.None(),
                    StandUpResult.PlayerNotSitting => new GameTurnPrompt.Reply(
                        "You are not sitting down."
                    ),
                    _ => throw new InvalidOperationException($"Unhandled stand result {result}."),
                };
            },
            cancellationToken
        );
}
