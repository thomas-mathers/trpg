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
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    ICommandHandler<BoardCaravanCommand, BoardCaravanResult> boardCaravan,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
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
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var result = await boardCaravan.Handle(
            new BoardCaravanCommand
            {
                PlayerId = session.PlayerId,
                CaravanId = caravanId,
                PlayerLocationId = player.LocationId,
                GameTime = gameTime,
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
        if (result.Outcome == BoardCaravanOutcome.TravelSuspended)
        {
            return new GameTurnPrompt.Reply(
                "The caravan has suspended passenger service until the weather improves. Your ticket remains valid."
            );
        }

        var arrivalGameTime = await advanceTime.Handle(
            new AdvanceTimeCommand
            {
                SessionId = session.SessionId,
                Delta = TimeSpan.FromHours(1) * result.TravelTimeHours!.Value,
            },
            cancellationToken
        );

        await movePlayer.Handle(
            new MovePlayerCommand
            {
                PlayerId = session.PlayerId,
                DestinationLocationId = result.DestinationLocationId!.Value,
                GameTime = arrivalGameTime,
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
