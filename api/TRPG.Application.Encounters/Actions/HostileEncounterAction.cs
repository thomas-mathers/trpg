namespace TRPG.Application.Encounters;

public abstract record HostileEncounterAction;

public sealed record AttackEncounterAction : HostileEncounterAction;

public sealed record EvadeEncounterAction : HostileEncounterAction;

public sealed record RetreatEncounterAction : HostileEncounterAction;
