using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class TrespassingConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : CrimeConsequenceResolver<TrespassingCrime>(pendingCrimeWitnessResolution, reputationOptions)
{
    public override ReputationReason FactionReason => ReputationReason.TrespassedOnFactionProperty;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedTrespassing;

    protected override CrimeReport ToCrimeReport(
        TrespassingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
