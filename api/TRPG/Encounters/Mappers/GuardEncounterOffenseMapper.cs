using TRPG.Domain.Models;

namespace TRPG.Encounters.Mappers;

internal static class GuardEncounterOffenseMapper
{
    // The guard is the one being briefed, so an offence against her reads in the second person.
    public static string ToNarratorText(this GuardEncounterOffense offense) =>
        $"{offense.Action} {(offense.SubjectIsTheGuard ? "you" : offense.SubjectName)}";

    // The player is reading a charge sheet about someone else, so the guard is always named.
    public static string ToPlayerText(this GuardEncounterOffense offense) =>
        $"{offense.Action} {offense.SubjectName}";
}
