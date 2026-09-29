using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamSleepTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SleepInRoomCommand, SleepInRoomResult> sleepInRoom,
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
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
            return new GameTurnPrompt.Reply("The sleep duration must be positive.");
        }
        if (delta > TimeSpan.FromHours(24))
        {
            return new GameTurnPrompt.Reply("You can sleep for at most 24 hours at a time.");
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        var outcome = await sleepInRoom.Handle(
            new SleepInRoomCommand
            {
                PlayerId = session.PlayerId,
                WorldId = session.WorldId,
                LocationId = player!.LocationId,
                Delta = delta,
            },
            cancellationToken
        );

        if (outcome.GameTime is { } gameTime)
        {
            var effectVitals = await advanceCreatureEffects.Handle(
                new AdvanceCreatureEffectsCommand
                {
                    WorldId = session.WorldId,
                    CreatureIds = [session.PlayerId],
                    GameTime = gameTime,
                },
                cancellationToken
            );
            if (effectVitals.HasDied(session.PlayerId))
            {
                return new GameTurnPrompt.Narrate(
                    "The player died from a lingering effect in their sleep. Narrate their death in two or three sentences.",
                    IncludeTools: false
                );
            }
            await refreshScene.Handle(
                new RefreshSceneCommand
                {
                    WorldId = session.WorldId,
                    PlayerId = session.PlayerId,
                    GameTime = gameTime,
                },
                cancellationToken
            );
        }

        return outcome.Outcome switch
        {
            SleepOutcome.NotYourRoom => new GameTurnPrompt.Reply(
                "There's no bed here that's rented to the player."
            ),
            _ => new GameTurnPrompt.Narrate(
                "A night's rest has passed. Narrate the player waking up refreshed and well-rested in their rented room, then call look now to describe their surroundings."
            ),
        };
    }
}
