using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class TheftCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this TheftCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) =>
        new(
            crime.OwnerFactionId == null ? [] : [crime.OwnerFactionId.Value],
            reportedWitnessIds,
            crime.Outcome == TheftCrimeOutcome.Apologized
                ? options.ApologizedTheftReputationPenalty
                : options.TheftReputationPenalty
        );
}
