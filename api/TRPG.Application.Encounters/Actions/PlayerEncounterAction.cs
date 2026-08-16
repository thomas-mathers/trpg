namespace TRPG.Application.Encounters;

public abstract record PlayerEncounterAction;

public sealed record AttackEncounterAction : PlayerEncounterAction;

public sealed record EvadeEncounterAction : PlayerEncounterAction;

public sealed record RetreatEncounterAction : PlayerEncounterAction;
