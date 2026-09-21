using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamBoardCaravanTurnHandler(
    GameTurnStreamer streamer,
    GameTurnContext turnContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<BoardCaravanCommand, BoardCaravanResult> boardCaravan,
    ICommandHandler<AdvanceTimeCommand, TimeSpan> advanceTime,
    ICommandHandler<MovePlayerCommand> movePlayer
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, caravanId, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        Guid caravanId,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = session.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), session.PlayerId);
        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await boardCaravan.Handle(
            new BoardCaravanCommand
            {
                PlayerId = session.PlayerId,
                CaravanId = caravanId,
                PlayerLocationId = player.LocationId,
                Playtime = playtime,
            },
            cancellationToken
        );

        if (result.Outcome == BoardCaravanOutcome.NoTicket)
        {
            return new GameTurnPrompt.Reply("You don't have a ticket for this caravan.");
        }
        if (result.Outcome == BoardCaravanOutcome.CaravanNotPresent)
        {
            return new GameTurnPrompt.Reply("The caravan isn't here anymore.");
        }

        var arrivalPlaytime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = session.SessionId,
                Delta = GameClock.RealTimePerInGameHour * result.TravelTimeHours!.Value,
            },
            cancellationToken
        );

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = session.PlayerId,
                DestinationLocationId = result.DestinationLocationId!.Value,
                Playtime = arrivalPlaytime,
            },
            cancellationToken
        );
        turnContext.PlayerMoved = true;

        return new GameTurnPrompt.Narrate(
            "The player just boarded the caravan and traveled to their destination. Narrate a "
                + "brief journey montage and the arrival in two or three sentences."
        );
    }
}
