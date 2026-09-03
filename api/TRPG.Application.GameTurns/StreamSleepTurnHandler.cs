using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.RoomBookings.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamSleepTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SleepInRoomCommand, SleepOutcome> sleepInRoom
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
            return new GameTurnPrompt.Reply("The sleep duration must be positive.");
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        var outcome = await sleepInRoom.Handle(
            new SleepInRoomCommand
            {
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
                LocationId = player!.LocationId,
                Delta = GameClock.RealTimePerInGameHour * (hours + minutes / 60.0),
            },
            cancellationToken
        );

        return outcome switch
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
