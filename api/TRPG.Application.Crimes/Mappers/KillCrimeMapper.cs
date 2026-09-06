using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class KillCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this KillCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => new(crime.VictimFactionIds, reportedWitnessIds, options.KillReputationPenalty);
}
