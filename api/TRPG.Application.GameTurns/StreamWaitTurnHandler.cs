using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameTurns.Commands;
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
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene
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
        var delta = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
        if (delta <= TimeSpan.Zero)
        {
            return new GameTurnPrompt.Reply("The wait duration must be positive.");
        }
        if (delta > TimeSpan.FromHours(24))
        {
            return new GameTurnPrompt.Reply("You can wait for at most 24 hours at a time.");
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
            new AdvanceTimeCommand { WorldId = session.WorldId, Delta = delta },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { GameTime = gameTime, CreatureIds = [session.PlayerId] },
            cancellationToken
        );

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
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
