using TRPG.Application.Combat.Events;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class ApplyCombatRoundOutcomeCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required CombatState State { get; init; }
}

internal class ApplyCombatRoundOutcomeCommandHandler(
    ICommandHandler<ApplyCombatUsageCommand> applyCombatUsage,
    IGameClientEventSink gameEvents,
    IDomainEventPublisher<CreatureKilledEvent> domainEvents
) : ICommandHandler<ApplyCombatRoundOutcomeCommand>
{
    public async Task Handle(
        ApplyCombatRoundOutcomeCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var state = command.State;

        gameEvents.Enqueue(
            new CombatUpdatedEvent(command.WorldId, state.Combatants, state.Events, state.Outcome)
        );

        await applyCombatUsage.Handle(
            new ApplyCombatUsageCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = command.GameTime,
                State = state,
            },
            cancellationToken
        );

        foreach (
            var combatant in command.Combatants.Where(combatant =>
                combatant is { IsPlayer: false, IsAlive: false }
            )
        )
        {
            await domainEvents.Publish(
                new CreatureKilledEvent(
                    command.PlayerId,
                    command.WorldId,
                    combatant.CreatureId,
                    combatant.CreatureType,
                    command.LocationId
                ),
                cancellationToken
            );
        }
    }
}
