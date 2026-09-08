using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class LockpickingCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this LockpickingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) =>
        new(
            FactionIds: crime.OwnerFactionIds,
            ReportedWitnessIds: reportedWitnessIds,
            VictimId: null,
            Penalty: PenaltyFor(crime, options)
        );

    private static int PenaltyFor(LockpickingCrime crime, ReputationOptions options) =>
        crime.Outcome == LockpickingCrimeOutcome.SettledWithGuard
            ? options.SettledLockpickingReputationPenalty
            : options.LockpickingReputationPenalty;
}
