using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Events;

namespace TRPG.Application.Combat.Events;

public record CombatStartedEvent(Guid FightId, IReadOnlyCollection<CombatantResult> Combatants)
    : GameClientEvent { }
