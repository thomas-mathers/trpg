using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Mappers;

internal static class AssaultCrimeMapper
{
    public static CrimeReport ToCrimeReport(
        this AssaultCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => new(crime.VictimFactionIds, reportedWitnessIds, options.AssaultReputationPenalty);
}
