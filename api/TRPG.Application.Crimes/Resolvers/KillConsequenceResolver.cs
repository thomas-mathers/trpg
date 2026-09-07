using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class KillConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
)
    : CrimeConsequenceResolver<KillCrime>(
        pendingCrimeWitnessResolution,
        recordCrimeHearsay,
        reputationOptions
    )
{
    public override ReputationReason FactionReason => ReputationReason.KilledFactionMember;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedKilling;

    protected override CrimeReport ToCrimeReport(
        KillCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
