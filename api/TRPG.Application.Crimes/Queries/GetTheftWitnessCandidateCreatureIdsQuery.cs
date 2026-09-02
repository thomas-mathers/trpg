using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class GetTheftWitnessCandidateCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetTheftWitnessCandidateCreatureIdsQueryHandler(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : IQueryHandler<GetTheftWitnessCandidateCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetTheftWitnessCandidateCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<TheftCrime>(
            query.WorldId,
            query.PlayerId,
            query.LocationId,
            cancellationToken
        );
}
