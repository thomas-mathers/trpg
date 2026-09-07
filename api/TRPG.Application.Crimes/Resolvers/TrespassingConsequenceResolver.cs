using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class TrespassingConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
)
    : CrimeConsequenceResolver<TrespassingCrime>(
        pendingCrimeWitnessResolution,
        recordCrimeHearsay,
        reputationOptions
    )
{
    public override ReputationReason FactionReason => ReputationReason.TrespassedOnFactionProperty;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedTrespassing;

    protected override CrimeReport ToCrimeReport(
        TrespassingCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
