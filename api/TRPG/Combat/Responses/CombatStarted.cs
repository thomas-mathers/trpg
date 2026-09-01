namespace TRPG.Combat.Responses;

[Tapper.TranspilationSource]
public record CombatStarted(Guid FightId, IReadOnlyCollection<CombatantState> Combatants);
