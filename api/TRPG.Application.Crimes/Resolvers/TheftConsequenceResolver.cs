using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class TheftConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : CrimeConsequenceResolver<TheftCrime>(pendingCrimeWitnessResolution, reputationOptions)
{
    public override ReputationReason FactionReason => ReputationReason.StoleFromFactionMember;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedTheft;

    protected override CrimeReport ToCrimeReport(
        TheftCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
