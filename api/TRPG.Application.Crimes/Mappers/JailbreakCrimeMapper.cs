using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class JailbreakCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this JailbreakCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) =>
        new(
            FactionIds: crime.OwnerFactionId == null ? [] : [crime.OwnerFactionId.Value],
            ReportedWitnessIds: reportedWitnessIds,
            // The city was wronged, not a person: the cell belonged to nobody in particular.
            VictimId: null,
            Penalty: crime.Outcome == LockpickingCrimeOutcome.SettledWithGuard
                ? options.SettledJailbreakReputationPenalty
                : options.JailbreakReputationPenalty
        );
}
