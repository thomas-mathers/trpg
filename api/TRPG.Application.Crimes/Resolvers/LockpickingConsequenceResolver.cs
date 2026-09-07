using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class LockpickingConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
)
    : CrimeConsequenceResolver<LockpickingCrime>(
        pendingCrimeWitnessResolution,
        recordCrimeHearsay,
        reputationOptions
    )
{
    public override ReputationReason FactionReason => ReputationReason.PickedFactionLock;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedLockpicking;

    protected override CrimeReport ToCrimeReport(
        LockpickingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
