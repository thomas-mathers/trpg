using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class GetTrespassingWitnessCandidateCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetTrespassingWitnessCandidateCreatureIdsQueryHandler(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : IQueryHandler<GetTrespassingWitnessCandidateCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetTrespassingWitnessCandidateCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<TrespassingCrime>(
            query.WorldId,
            query.PlayerId,
            query.LocationId,
            cancellationToken
        );
}
