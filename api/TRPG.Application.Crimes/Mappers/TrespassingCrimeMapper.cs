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
            crime.OwnerFactionId == null ? [] : [crime.OwnerFactionId.Value],
            reportedWitnessIds,
            options.TrespassingReputationPenalty
        );
}
