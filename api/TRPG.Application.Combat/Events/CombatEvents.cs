using TRPG.Application.Common.Events;

namespace TRPG.Application.Combat.Events;

public record CombatStartedEvent(IReadOnlyCollection<Combatant> Combatants) : GameClientEvent { }

public record CombatUpdatedEvent(
    IReadOnlyCollection<Combatant> Combatants,
    IReadOnlyList<CombatResolution> Events,
    TRPG.Domain.Models.CombatOutcome Outcome
) : GameClientEvent { }
