using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Worlds.Commands;
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
    ICommandHandler<
        AdvanceCreatureEffectsCommand,
        IReadOnlyCollection<CreatureVitals>
    > advanceCreatureEffects,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
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
                WorldId = session.WorldId,
                Delta = TimeSpan.FromHours(1) * result.TravelTimeHours!.Value,
            },
            cancellationToken
        );

        var effectVitals = await advanceCreatureEffects.Handle(
            new AdvanceCreatureEffectsCommand
            {
                WorldId = session.WorldId,
                CreatureIds = [session.PlayerId],
                GameTime = arrivalGameTime,
            },
            cancellationToken
        );
        if (effectVitals.HasDied(session.PlayerId))
        {
            return new GameTurnPrompt.Narrate(
                "The player died from a lingering effect during the caravan journey and never arrived. Narrate their death in two or three sentences.",
                IncludeTools: false
            );
        }

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                GameTime = arrivalGameTime,
                CreatureIds = [session.PlayerId],
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
