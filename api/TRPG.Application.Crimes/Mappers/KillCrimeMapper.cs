using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class KillCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this KillCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) =>
        new(
            FactionIds: crime.VictimFactionIds,
            ReportedWitnessIds: reportedWitnessIds,
            // The victim is a corpse, so there is nobody left to hold a personal grudge.
            VictimId: null,
            Penalty: options.KillReputationPenalty
        );
}
