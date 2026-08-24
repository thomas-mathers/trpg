namespace TRPG.Combat.Responses;

[Tapper.TranspilationSource]
public record CombatStartedPayload(Guid FightId, IReadOnlyCollection<CombatantState> Combatants);
