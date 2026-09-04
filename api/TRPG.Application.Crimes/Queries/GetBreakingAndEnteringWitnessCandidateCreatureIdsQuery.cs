using TRPG.Application.Common.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Queries;

public class GetBreakingAndEnteringWitnessCandidateCreatureIdsQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetBreakingAndEnteringWitnessCandidateCreatureIdsQueryHandler(
    PendingCrimeWitnessResolutionService pendingCrimeWitnessResolution
) : IQueryHandler<GetBreakingAndEnteringWitnessCandidateCreatureIdsQuery, IReadOnlyCollection<Guid>>
{
    public async Task<IReadOnlyCollection<Guid>> Handle(
        GetBreakingAndEnteringWitnessCandidateCreatureIdsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await pendingCrimeWitnessResolution.GetWitnessCandidateCreatureIds<BreakingAndEnteringCrime>(
            query.WorldId,
            query.PlayerId,
            query.LocationId,
            cancellationToken
        );
}
