using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Factions.Queries;

public sealed record NameSimilarityMatch(Guid Id, string Name, double Similarity);

public class GetFactionsRankedBySimilarityQuery
{
    public required IReadOnlyCollection<Guid> CandidateIds { get; init; }
    public required string SearchName { get; init; }
    public required double SimilarityThreshold { get; init; }
    public required int MaxMatches { get; init; }
}

internal class GetFactionsRankedBySimilarityQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetFactionsRankedBySimilarityQuery, IReadOnlyList<NameSimilarityMatch>>
{
    public async Task<IReadOnlyList<NameSimilarityMatch>> Handle(
        GetFactionsRankedBySimilarityQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Factions.AsNoTracking()
            .Where(f => query.CandidateIds.AsEnumerable().Contains(f.Id))
            .Select(f => new
            {
                f.Id,
                f.Name,
                Similarity = EF.Functions.TrigramsStrictWordSimilarity(query.SearchName, f.Name),
            })
            .Where(x => x.Similarity >= query.SimilarityThreshold)
            .OrderByDescending(x => x.Similarity)
            .Take(query.MaxMatches)
            .Select(x => new NameSimilarityMatch(x.Id, x.Name, x.Similarity))
            .ToArrayAsync(cancellationToken);
}
