using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Queries;

internal class GetCreatureKnowledgeQuery
{
    public required Guid WorldId { get; init; }
    public required string SubjectName { get; init; }
    public required int CurrentYear { get; init; }
    public required Creature AskingPerson { get; init; }
}

internal sealed record LookupMatch(
    double Similarity,
    string Name,
    string SubjectType,
    LookupResult? Result
);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "subjectType")]
[JsonDerivedType(typeof(CountryLookupResult), "country")]
[JsonDerivedType(typeof(CityLookupResult), "city")]
[JsonDerivedType(typeof(FactionLookupResult), "faction")]
[JsonDerivedType(typeof(PersonLookupResult), "person")]
internal abstract record LookupResult;

internal sealed record CountryLookupResult(
    string Name,
    string Description,
    string Focus,
    string DominantRace,
    string? CapitalCityName
) : LookupResult;

internal sealed record CityDistrictInfo(string Name, string Type);

internal sealed record CityLookupResult(
    string Name,
    string Description,
    bool IsCapital,
    string? StateName,
    string? CountryName,
    IReadOnlyCollection<CityDistrictInfo> Districts,
    int PopulationCount
) : LookupResult;

internal sealed record FactionLookupResult(
    string Name,
    string Description,
    string? LeaderName,
    int MemberCount
) : LookupResult;

internal sealed record RelativeInfo(string Name, string RelationshipType);

internal sealed record PersonLookupResult(
    string Name,
    string CreatureType,
    string Gender,
    string? Profession,
    int Level,
    int Age,
    string Biography,
    IReadOnlyCollection<string> FactionNames,
    IReadOnlyCollection<RelativeInfo> Relatives,
    string? StateName,
    string? CityName,
    string? DistrictName
) : LookupResult;

internal class GetCreatureKnowledgeQueryHandler(TrpgDbContext context)
{
    private const double SimilarityThreshold = 0.35;
    private const int MaxMatches = 5;

    private sealed record Candidate(
        double Similarity,
        string Name,
        KnowledgeSubjectType SubjectType,
        Guid EntityId
    );

    public async Task<IReadOnlyList<LookupMatch>> Handle(
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var candidates = new List<Candidate>();
        candidates.AddRange(
            await FindCandidates(
                query,
                context.Countries,
                KnowledgeSubjectType.Country,
                cancellationToken
            )
        );
        candidates.AddRange(
            await FindCandidates(
                query,
                context.Cities,
                KnowledgeSubjectType.City,
                cancellationToken
            )
        );
        candidates.AddRange(
            await FindCandidates(
                query,
                context.Factions,
                KnowledgeSubjectType.Faction,
                cancellationToken
            )
        );
        candidates.AddRange(
            await FindCandidates(
                query,
                context.Creatures,
                KnowledgeSubjectType.Creature,
                cancellationToken
            )
        );

        var ranked = candidates.OrderByDescending(c => c.Similarity).Take(MaxMatches).ToList();

        var matches = new List<LookupMatch>();
        foreach (var candidate in ranked)
        {
            var result =
                matches.Count == 0 ? await BuildResult(candidate, query, cancellationToken) : null;
            matches.Add(
                new LookupMatch(
                    candidate.Similarity,
                    candidate.Name,
                    candidate.SubjectType.ToString(),
                    result
                )
            );
        }

        return matches;
    }

    private async Task<IReadOnlyList<Candidate>> FindCandidates<TEntity>(
        GetCreatureKnowledgeQuery query,
        IQueryable<TEntity> entities,
        KnowledgeSubjectType subjectType,
        CancellationToken cancellationToken
    )
        where TEntity : class
    {
        return await context
            .CreatureKnowledge.AsNoTracking()
            .Where(k =>
                k.KnowerId == query.AskingPerson.Id
                && k.SubjectType == subjectType
                && k.SubjectId != query.AskingPerson.Id
            )
            .Join(
                entities.AsNoTracking(),
                k => k.SubjectId,
                e => EF.Property<Guid>(e, "Id"),
                (k, e) => e
            )
            .Select(e => new
            {
                Name = EF.Property<string>(e, "Name"),
                Id = EF.Property<Guid>(e, "Id"),
                Similarity = EF.Functions.TrigramsStrictWordSimilarity(
                    query.SubjectName,
                    EF.Property<string>(e, "Name")
                ),
            })
            .Where(x => x.Similarity >= SimilarityThreshold)
            .OrderByDescending(x => x.Similarity)
            .Take(MaxMatches)
            .Select(x => new Candidate(x.Similarity, x.Name, subjectType, x.Id))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<LookupResult?> BuildResult(
        Candidate candidate,
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken
    )
    {
        switch (candidate.SubjectType)
        {
            case KnowledgeSubjectType.Country:
                var country = await context
                    .Countries.AsNoTracking()
                    .FirstAsync(c => c.Id == candidate.EntityId, cancellationToken);
                return await BuildCountryResult(country, cancellationToken);
            case KnowledgeSubjectType.City:
                var city = await context
                    .Cities.AsNoTracking()
                    .FirstAsync(c => c.Id == candidate.EntityId, cancellationToken);
                return await BuildCityResult(city, query.WorldId, cancellationToken);
            case KnowledgeSubjectType.Faction:
                var faction = await context
                    .Factions.AsNoTracking()
                    .FirstAsync(f => f.Id == candidate.EntityId, cancellationToken);
                return await BuildFactionResult(faction, cancellationToken);
            case KnowledgeSubjectType.Creature:
                var creature = await context
                    .Creatures.AsNoTracking()
                    .FirstAsync(c => c.Id == candidate.EntityId, cancellationToken);
                return await BuildPersonResult(creature, query.CurrentYear, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(candidate));
        }
    }

    private async Task<CountryLookupResult> BuildCountryResult(
        Country country,
        CancellationToken cancellationToken
    )
    {
        var capital = await (
            from city in context.Cities.AsNoTracking()
            join state in context.States on city.StateId equals state.Id
            where state.CountryId == country.Id && city.IsCapital
            select city
        ).FirstOrDefaultAsync(cancellationToken);

        return new CountryLookupResult(
            country.Name,
            country.Description,
            country.Focus.ToString(),
            country.DominantRace.ToString(),
            capital?.Name
        );
    }

    private async Task<CityLookupResult> BuildCityResult(
        City city,
        Guid worldId,
        CancellationToken cancellationToken
    )
    {
        var state = await context
            .States.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == city.StateId, cancellationToken);
        var country =
            state != null
                ? await context
                    .Countries.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == state.CountryId, cancellationToken)
                : null;
        var districts = await context
            .Districts.AsNoTracking()
            .Where(d => d.CityId == city.Id)
            .ToArrayAsync(cancellationToken);
        var populationCount = await context.Creatures.CountAsync(
            p => p.WorldId == worldId && p.CityId == city.Id,
            cancellationToken
        );

        return new CityLookupResult(
            city.Name,
            city.Description,
            city.IsCapital,
            state?.Name,
            country?.Name,
            districts
                .Select(d => new CityDistrictInfo(d.Name, d.DistrictType.ToString()))
                .ToArray(),
            populationCount
        );
    }

    private async Task<FactionLookupResult> BuildFactionResult(
        Faction faction,
        CancellationToken cancellationToken
    )
    {
        var leaderName = await (
            from fm in context.FactionMembers
            where fm.FactionId == faction.Id && fm.Role == FactionRole.Leader
            join c in context.Creatures on fm.CreatureId equals c.Id
            select c.Name
        ).FirstOrDefaultAsync(cancellationToken);
        var memberCount = await context.FactionMembers.CountAsync(
            fm => fm.FactionId == faction.Id,
            cancellationToken
        );

        return new FactionLookupResult(faction.Name, faction.Description, leaderName, memberCount);
    }

    private async Task<PersonLookupResult> BuildPersonResult(
        Creature creature,
        int currentYear,
        CancellationToken cancellationToken
    )
    {
        var state = await context
            .States.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == creature.StateId, cancellationToken);
        var city = creature.CityId is { } cityId
            ? await context
                .Cities.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cityId, cancellationToken)
            : null;
        var district = creature.DistrictId is { } districtId
            ? await context
                .Districts.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == districtId, cancellationToken)
            : null;
        var factionNames = await (
            from fm in context.FactionMembers
            where fm.CreatureId == creature.Id
            join f in context.Factions on fm.FactionId equals f.Id
            where !f.IsCityFaction
            select f.Name
        ).ToArrayAsync(cancellationToken);

        var relatives = await (
            from r in context.Relationships
            where r.SubjectId == creature.Id
            join relative in context.Creatures on r.RelativeId equals relative.Id
            select new RelativeInfo(relative.Name, r.RelationshipType.ToString())
        ).ToArrayAsync(cancellationToken);

        return new PersonLookupResult(
            creature.Name,
            creature.CreatureType.ToString(),
            creature.Gender.ToString(),
            creature.Profession?.ToString(),
            creature.Level,
            currentYear - creature.BirthYear,
            creature.Biography,
            factionNames,
            relatives,
            state?.Name,
            city?.Name,
            district?.Name
        );
    }
}
