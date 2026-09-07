using TRPG.Application.Crimes.Queries;

namespace TRPG.Application.Encounters.Mappers;

internal static class OutstandingCrimeMapper
{
    public static string ToOffenseText(this OutstandingCrime crime, Guid guardCreatureId)
    {
        var subject = crime.SubjectCreatureId == guardCreatureId ? "you" : crime.SubjectName;

        return crime.Kind switch
        {
            OutstandingCrimeKind.Kill => $"Killed {subject}",
            OutstandingCrimeKind.Assault => $"Assaulted {subject}",
            OutstandingCrimeKind.Theft => ToTheftText(crime, subject),
            OutstandingCrimeKind.Lockpicking => crime.IsJailbreak
                ? $"Broke out of {crime.SubjectName}"
                : $"Broke into {crime.SubjectName}",
            OutstandingCrimeKind.Trespassing => $"Trespassed in {crime.SubjectName}",
        };
    }

    private static string ToTheftText(OutstandingCrime crime, string subject) =>
        crime.ItemNames.Count == 0
            ? $"Stole from {subject}"
            : $"Stole {string.Join(", ", crime.ItemNames)} from {subject}";
}
