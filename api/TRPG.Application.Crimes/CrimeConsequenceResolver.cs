using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes;

public record CrimeScope(Guid WorldId, Guid PlayerId, Guid LocationId);

public interface ICrimeConsequenceResolver
{
    ReputationReason FactionReason { get; }
    ReputationReason WitnessReason { get; }

    // Null for crimes whose only injured party is a faction or a corpse.
    ReputationReason? VictimReason { get; }

    Task<IReadOnlyCollection<Guid>> GetWitnessCandidates(
        CrimeScope scope,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyCollection<CrimeReport>> Resolve(
        CrimeScope scope,
        IReadOnlyCollection<Guid> liveWitnessCreatureIds,
        CancellationToken cancellationToken = default
    );
}

// One crime type's consequences. Subclasses supply only their reasons and their pricing.
internal abstract class CrimeConsequenceResolver<TCrime>(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution,
    ICommandHandler<RecordCrimeHearsayCommand> recordCrimeHearsay,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICrimeConsequenceResolver
    where TCrime : Crime
{
    public abstract ReputationReason FactionReason { get; }
    public abstract ReputationReason WitnessReason { get; }
    public virtual ReputationReason? VictimReason => null;

    protected abstract CrimeReport ToCrimeReport(
        TCrime crime,
        IReadOnlyCollection<Guid> reportedWitnessIds,
        ReputationOptions options
    );

    public async Task<IReadOnlyCollection<Guid>> GetWitnessCandidates(
        CrimeScope scope,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<TCrime>(
            scope.WorldId,
            scope.PlayerId,
            scope.LocationId,
            cancellationToken
        );

    public async Task<IReadOnlyCollection<CrimeReport>> Resolve(
        CrimeScope scope,
        IReadOnlyCollection<Guid> liveWitnessCreatureIds,
        CancellationToken cancellationToken = default
    )
    {
        var resolution = await pendingCrimeWitnessResolution.Resolve<TCrime>(
            scope.WorldId,
            scope.PlayerId,
            scope.LocationId,
            liveWitnessCreatureIds,
            cancellationToken
        );

        var options = reputationOptions.CurrentValue;

        var reportsByCrimeId = resolution.ReportedCrimes.ToDictionary(
            crime => crime.Id,
            crime =>
                ToCrimeReport(crime, resolution.ReportingWitnessIdsByCrimeId[crime.Id], options)
        );

        await RecordVictimHearsay(scope.WorldId, reportsByCrimeId, cancellationToken);

        return reportsByCrimeId.Values.ToArray();
    }

    // Whoever reported it names the culprit to the victim, so an absent victim still learns of it.
    private async Task RecordVictimHearsay(
        Guid worldId,
        IReadOnlyDictionary<Guid, CrimeReport> reportsByCrimeId,
        CancellationToken cancellationToken
    )
    {
        var entries = reportsByCrimeId
            .Where(entry => entry.Value.VictimId != null)
            .Select(entry => new CrimeHearsay(entry.Key, entry.Value.VictimId!.Value))
            .ToArray();

        await recordCrimeHearsay.Handle(
            new RecordCrimeHearsayCommand { WorldId = worldId, Entries = entries },
            cancellationToken
        );
    }
}
