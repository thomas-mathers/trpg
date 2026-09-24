using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns.Commands;

namespace TRPG.Application.GameTurns;

internal class StreamSitDownTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<SitDownCommand, SitDownResult> sitDown
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid seatId,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            async ct =>
            {
                var result = await sitDown.Handle(
                    new SitDownCommand { PlayerId = session.PlayerId, SeatId = seatId },
                    ct
                );
                return result switch
                {
                    SitDownResult.Success => new GameTurnPrompt.None(),
                    SitDownResult.SeatOccupied => new GameTurnPrompt.Reply(
                        "That seat is already occupied."
                    ),
                    SitDownResult.SeatNotNearby => new GameTurnPrompt.Reply(
                        "That seat is not nearby."
                    ),
                    SitDownResult.PlayerNotIdle => new GameTurnPrompt.Reply(
                        "You cannot sit down right now."
                    ),
                    _ => throw new InvalidOperationException($"Unhandled sit result {result}."),
                };
            },
            cancellationToken
        );
}
