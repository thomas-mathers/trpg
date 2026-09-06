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
            crime.OwnerFactionId == null ? [] : [crime.OwnerFactionId.Value],
            reportedWitnessIds,
            PenaltyFor(crime, options)
        );

    // Escaping custody outranks settling: going quietly discounts it but never to a shop door.
    private static int PenaltyFor(LockpickingCrime crime, ReputationOptions options)
    {
        var settled = crime.Outcome == LockpickingCrimeOutcome.SettledWithGuard;

        if (crime.IsJailbreak)
        {
            return settled
                ? options.SettledJailbreakReputationPenalty
                : options.JailbreakReputationPenalty;
        }

        return settled
            ? options.SettledLockpickingReputationPenalty
            : options.LockpickingReputationPenalty;
    }
}
