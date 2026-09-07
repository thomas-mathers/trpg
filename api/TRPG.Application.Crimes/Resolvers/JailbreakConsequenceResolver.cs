using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Mappers;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Resolvers;

internal sealed class JailbreakConsequenceResolver(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
)
    : CrimeConsequenceResolver<JailbreakCrime>(
        pendingCrimeWitnessResolution,
        recordCrimeHearsay,
        reputationOptions
    )
{
    public override ReputationReason FactionReason => ReputationReason.BrokeOutOfJail;
    public override ReputationReason WitnessReason => ReputationReason.WitnessedJailbreak;

    protected override CrimeReport ToCrimeReport(
        JailbreakCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    ) => crime.ToCrimeReport(reportedWitnessIds, options);
}
