using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Events;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Application.Combat.Events;

internal record CombatStartedEvent(
    IReadOnlyCollection<TRPG.Contracts.Combat.Responses.CombatantState> Combatants
) : GameClientEvent
{
    public override string MethodName => "CombatStarted";
    public override object? Payload => Combatants;
}

internal record CombatUpdatedEvent(
    IReadOnlyCollection<TRPG.Contracts.Combat.Responses.CombatantState> Combatants,
    IReadOnlyList<CombatRoundEvent> Events,
    TRPG.Data.Models.CombatOutcome Outcome
) : GameClientEvent
{
    public override string MethodName => "CombatUpdated";
    public override object? Payload =>
        new CombatUpdatePayload(Combatants, Events, Outcome.ToContract());
}
