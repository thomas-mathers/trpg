using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class AssaultConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
)
    : CrimeConsequenceResolver<AssaultCrime>(
        pendingCrimeWitnessResolution,
        recordCrimeHearsay,
        reputationOptions
    )
{
    public override ReputationReason FactionReason => ReputationReason.AssaultedFactionMember;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedAssault;
    public override ReputationReason? VictimReason => ReputationReason.AssaultedVictim;

    protected override CrimeReport ToCrimeReport(
        AssaultCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
