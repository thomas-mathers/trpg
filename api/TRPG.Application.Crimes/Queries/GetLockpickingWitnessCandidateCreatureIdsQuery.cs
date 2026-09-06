using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class GetLockpickingWitnessCandidateCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetLockpickingWitnessCandidateCreatureIdsQueryHandler(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : IQueryHandler<GetLockpickingWitnessCandidateCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetLockpickingWitnessCandidateCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<LockpickingCrime>(
            query.WorldId,
            query.PlayerId,
            query.LocationId,
            cancellationToken
        );
}
