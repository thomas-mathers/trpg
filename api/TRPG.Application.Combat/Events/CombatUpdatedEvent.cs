using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Events;

public record CombatUpdatedEvent(
    Guid WorldId,
    IReadOnlyCollection<CombatantResult> Combatants,
    IReadOnlyList<CombatResolution> Events,
    CombatOutcome Outcome
) : GameClientEvent(WorldId);
