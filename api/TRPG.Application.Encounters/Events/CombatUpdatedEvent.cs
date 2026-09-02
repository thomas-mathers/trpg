using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Encounters.Events;

public record CombatUpdatedEvent(
    IReadOnlyCollection<CombatantResult> Combatants,
    IReadOnlyList<CombatResolution> Events,
    TRPG.Domain.Models.CombatOutcome Outcome
) : GameClientEvent { }
