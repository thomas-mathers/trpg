namespace TRPG.Application.Encounters;

public abstract record SuspicionEncounterAction;

public sealed record ComplySuspicionAction : SuspicionEncounterAction;

public sealed record FleeSuspicionAction : SuspicionEncounterAction;
