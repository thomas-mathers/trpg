using TRPG.Application.Crimes.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Mappers;

internal static class OutstandingCrimeMapper
{
    public static GuardEncounterOffense ToOffense(
        this OutstandingCrime crime,
        Guid guardCreatureId
    ) =>
        new(
            ToAction(crime),
            crime.SubjectName,
            SubjectIsTheGuard: crime.SubjectCreatureId == guardCreatureId
        );

    private static string ToAction(OutstandingCrime crime) =>
        crime.Kind switch
        {
            OutstandingCrimeKind.Kill => "Killed",
            OutstandingCrimeKind.Assault => "Assaulted",
            OutstandingCrimeKind.Theft => ToTheftAction(crime),
            OutstandingCrimeKind.Lockpicking => "Broke into",
            OutstandingCrimeKind.Jailbreak => "Broke out of",
            OutstandingCrimeKind.Trespassing => "Trespassed in",
        };

    private static string ToTheftAction(OutstandingCrime crime) =>
        crime.ItemNames.Count == 0
            ? "Stole from"
            : $"Stole {string.Join(", ", crime.ItemNames)} from";
}
