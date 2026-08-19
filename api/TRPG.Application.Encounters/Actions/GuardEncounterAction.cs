namespace TRPG.Application.Encounters;

public abstract record GuardEncounterAction;

public sealed record PayFineEncounterAction : GuardEncounterAction;

public sealed record GoToJailEncounterAction : GuardEncounterAction;

public sealed record ResistArrestEncounterAction : GuardEncounterAction;
