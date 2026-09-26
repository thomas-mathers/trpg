using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamWaitTurnHandler(
    GameTurnStreamer streamer,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session, hours, minutes, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        int hours,
        int minutes,
        CancellationToken cancellationToken
    )
    {
        if (hours < 0 || minutes < 0 || (hours == 0 && minutes == 0))
        {
            return new GameTurnPrompt.Reply("The wait duration must be positive.");
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );
        if (player?.State != CreatureState.Sitting)
        {
            return new GameTurnPrompt.Reply("You need to sit down before waiting.");
        }

        var gameTime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = session.SessionId,
                Delta = TimeSpan.FromHours(1) * (hours + minutes / 60.0),
            },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { GameTime = gameTime, CreatureIds = [session.PlayerId] },
            cancellationToken
        );

        var elapsed = string.Join(
            " and ",
            new[]
            {
                hours > 0 ? $"{hours} hour(s)" : null,
                minutes > 0 ? $"{minutes} minute(s)" : null,
            }.Where(part => part != null)
        );

        return new GameTurnPrompt.Narrate(
            $"{elapsed} have passed. Call look now, then narrate the passage of time and the player's surroundings based on what it returns."
        );
    }
}
