using TRPG.Domain.Models;

namespace TRPG.Encounters.Mappers;

internal static class GuardEncounterOffenseMapper
{
    // Always names the victim: "you" already means the player everywhere else the narrator writes,
    // so spending it on the guard leaves the sentence ambiguous. The narrator is told which
    // offences were personal by a separate flag instead.
    public static string ToText(this GuardEncounterOffense offense) =>
        $"{offense.Action} {offense.SubjectName}";
}
