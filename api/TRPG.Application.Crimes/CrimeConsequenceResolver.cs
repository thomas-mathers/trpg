using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes;

public record CrimeScope(Guid WorldId, Guid PlayerId, Guid LocationId);

public interface ICrimeConsequenceResolver
{
    ReputationReason FactionReason { get; }
    ReputationReason WitnessReason { get; }

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
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICrimeConsequenceResolver
    where TCrime : Crime
{
    public abstract ReputationReason FactionReason { get; }
    public abstract ReputationReason WitnessReason { get; }

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

        return resolution
            .ReportedCrimes.Select(crime =>
                ToCrimeReport(crime, resolution.ReportingWitnessIdsByCrimeId[crime.Id], options)
            )
            .ToArray();
    }
}
