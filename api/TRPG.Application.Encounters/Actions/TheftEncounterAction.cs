namespace TRPG.Application.Encounters;

public abstract record TheftEncounterAction;

public sealed record ApologizeTheftEncounterAction : TheftEncounterAction;

public sealed record FleeTheftEncounterAction : TheftEncounterAction;
