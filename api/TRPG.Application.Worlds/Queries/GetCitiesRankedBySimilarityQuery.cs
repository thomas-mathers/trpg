using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Queries;

public class GetCitiesRankedBySimilarityQuery
{
    public required IReadOnlyCollection<Guid> CandidateIds { get; init; }
    public required string SearchName { get; init; }
    public required double SimilarityThreshold { get; init; }
    public required int MaxMatches { get; init; }
}

internal class GetCitiesRankedBySimilarityQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetCitiesRankedBySimilarityQuery, IReadOnlyList<NameSimilarityMatch>>
{
    public async Task<IReadOnlyList<NameSimilarityMatch>> Handle(
        GetCitiesRankedBySimilarityQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Cities.AsNoTracking()
            .Where(c => query.CandidateIds.AsEnumerable().Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.Name,
                Similarity = EF.Functions.TrigramsStrictWordSimilarity(query.SearchName, c.Name),
            })
            .Where(x => x.Similarity >= query.SimilarityThreshold)
            .OrderByDescending(x => x.Similarity)
            .Take(query.MaxMatches)
            .Select(x => new NameSimilarityMatch(x.Id, x.Name, x.Similarity))
            .ToArrayAsync(cancellationToken);
}
