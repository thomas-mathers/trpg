using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamWaitTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<AdvanceTimeCommand, TimeSpan> advanceTime
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        int hours,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, hours, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        int hours,
        CancellationToken cancellationToken
    )
    {
        if (hours <= 0)
        {
            return new GameTurnPrompt.Reply("The number of hours to wait must be positive.");
        }

        await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = session.SessionId,
                Delta = hours * GameClock.RealTimePerInGameHour,
            },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = session.SessionId,
                CreatureIds = [session.PlayerId],
            },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            $"{hours} hour(s) have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns."
        );
    }
}
