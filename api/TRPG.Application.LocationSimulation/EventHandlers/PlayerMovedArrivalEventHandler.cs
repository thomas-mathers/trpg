using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.EventHandlers;

internal sealed class PlayerMovedArrivalEventHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<CatchUpLocationCommand, bool> catchUpLocation,
    ICommandHandler<EvaluateEncountersCommand, EncounterEvaluationResult> evaluateEncounters
) : IDomainEventConsumer<PlayerMovedEvent>
{
    public async Task Handle(
        PlayerMovedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = domainEvent.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), domainEvent.PlayerId);

        // Encounters are evaluated against whoever is present, so the destination has to be caught
        // up on spawning and scheduled movement before anything is evaluated against it.
        await catchUpLocation.Handle(
            new CatchUpLocationCommand
            {
                WorldId = domainEvent.WorldId,
                LocationId = domainEvent.ToLocationId,
                CurrentDate = GameClock.GetCurrentInGameDate(domainEvent.Playtime),
                PlayerLevel = player.Level,
                Playtime = domainEvent.Playtime,
            },
            cancellationToken
        );

        await evaluateEncounters.Handle(
            new EvaluateEncountersCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
            },
            cancellationToken
        );
    }
}
