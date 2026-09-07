using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class TrespassingCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this TrespassingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) =>
        new(
            FactionIds: crime.OwnerFactionId == null ? [] : [crime.OwnerFactionId.Value],
            ReportedWitnessIds: reportedWitnessIds,
            VictimId: null,
            Penalty: options.TrespassingReputationPenalty
        );
}
