using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class AssaultConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : CrimeConsequenceResolver<AssaultCrime>(pendingCrimeWitnessResolution, reputationOptions)
{
    public override ReputationReason FactionReason => ReputationReason.AssaultedFactionMember;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedAssault;

    protected override CrimeReport ToCrimeReport(
        AssaultCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
