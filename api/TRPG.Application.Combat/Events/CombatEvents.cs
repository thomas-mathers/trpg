using TRPG.Application.Combat.Mappers;
using TRPG.Application.Combat.Responses;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Combat.Events;

internal record CombatStartedEvent(
    IReadOnlyCollection<TRPG.Application.Combat.Responses.CombatantState> Combatants
) : GameClientEvent
{
    public override string MethodName => "CombatStarted";
    public override object? Payload => Combatants;
}

internal record CombatUpdatedEvent(
    IReadOnlyCollection<TRPG.Application.Combat.Responses.CombatantState> Combatants,
    IReadOnlyList<CombatRoundEvent> Events,
    TRPG.Domain.Models.CombatOutcome Outcome
) : GameClientEvent
{
    public override string MethodName => "CombatUpdated";
    public override object? Payload =>
        new CombatUpdatePayload(Combatants, Events, Outcome.ToContract());
}
