using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class LockpickingConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : CrimeConsequenceResolver<LockpickingCrime>(pendingCrimeWitnessResolution, reputationOptions)
{
    public override ReputationReason FactionReason => ReputationReason.PickedFactionLock;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedLockpicking;

    protected override CrimeReport ToCrimeReport(
        LockpickingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
