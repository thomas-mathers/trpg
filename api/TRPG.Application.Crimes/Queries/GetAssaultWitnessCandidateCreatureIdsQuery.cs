using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class GetAssaultWitnessCandidateCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetAssaultWitnessCandidateCreatureIdsQueryHandler(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : IQueryHandler<GetAssaultWitnessCandidateCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetAssaultWitnessCandidateCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<AssaultCrime>(
            query.WorldId,
            query.PlayerId,
            query.LocationId,
            cancellationToken
        );
}
