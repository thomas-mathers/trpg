using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Combat.Events;

public record CombatStartedEvent(IReadOnlyCollection<CombatantResult> Combatants)
    : GameClientEvent { }

public record CombatUpdatedEvent(
    IReadOnlyCollection<CombatantResult> Combatants,
    IReadOnlyList<CombatResolution> Events,
    TRPG.Domain.Models.CombatOutcome Outcome
) : GameClientEvent { }
