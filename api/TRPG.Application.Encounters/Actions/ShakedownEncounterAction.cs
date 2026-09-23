namespace TRPG.Application.Encounters;

public abstract record ShakedownEncounterAction;

public sealed record IntimidateEncounterAction : ShakedownEncounterAction;

public sealed record PayTollEncounterAction : ShakedownEncounterAction;

public sealed record FightEncounterAction : ShakedownEncounterAction;

public sealed record FleeShakedownEncounterAction : ShakedownEncounterAction;
