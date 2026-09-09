namespace TRPG.Application.Encounters;

public abstract record TrapEncounterAction;

public sealed record AttemptTrapAction : TrapEncounterAction;

public sealed record WithdrawTrapAction : TrapEncounterAction;

public sealed record DisarmTrapAction : TrapEncounterAction;
