using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using CreaturesMatch = TRPG.Application.Creatures.Queries.NameSimilarityMatch;
using FactionsMatch = TRPG.Application.Factions.Queries.NameSimilarityMatch;
using WorldsMatch = TRPG.Application.Worlds.Queries.NameSimilarityMatch;

namespace TRPG.Application.Knowledge.Queries;

public class GetCreatureKnowledgeQuery
{
    public required Guid WorldId { get; init; }
    public required string SubjectName { get; init; }
    public required int CurrentYear { get; init; }
    public required Creature AskingPerson { get; init; }
}

public sealed record LookupMatch(
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
public abstract record LookupResult;

public sealed record CountryLookupResult(
    string Name,
    string Description,
    string Focus,
    string DominantRace,
    string? CapitalCityName
) : LookupResult;

public sealed record CityDistrictInfo(string Name, string Type);

public sealed record CityLookupResult(
    string Name,
    string Description,
    bool IsCapital,
    string? StateName,
    string? CountryName,
    IReadOnlyCollection<CityDistrictInfo> Districts,
    int PopulationCount
) : LookupResult;

public sealed record FactionLookupResult(
    string Name,
    string Description,
    string? LeaderName,
    int MemberCount
) : LookupResult;

public sealed record RelativeInfo(string Name, string RelationshipType);

public sealed record PersonLookupResult(
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

internal class GetCreatureKnowledgeQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetRelativesQuery, IReadOnlyCollection<RelativeSummary>> getRelatives,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetStateByIdQuery, State?> getStateById,
    IQueryHandler<GetCityByIdQuery, City?> getCityById,
    IQueryHandler<GetDistrictByIdQuery, District?> getDistrictById,
    IQueryHandler<GetCountryByIdQuery, Country?> getCountryById,
    IQueryHandler<GetCapitalCityByCountryIdQuery, City?> getCapitalCityByCountryId,
    IQueryHandler<GetDistrictsByCityIdQuery, IReadOnlyCollection<District>> getDistrictsByCityId,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<
        GetCountriesRankedBySimilarityQuery,
        IReadOnlyList<WorldsMatch>
    > getCountriesRankedBySimilarity,
    IQueryHandler<
        GetCitiesRankedBySimilarityQuery,
        IReadOnlyList<WorldsMatch>
    > getCitiesRankedBySimilarity,
    IQueryHandler<GetFactionByIdQuery, Faction?> getFactionById,
    IQueryHandler<GetFactionLeadershipQuery, FactionLeadership> getFactionLeadership,
    IQueryHandler<
        GetNonCityFactionNamesByCreatureIdQuery,
        IReadOnlyList<string>
    > getNonCityFactionNamesByCreatureId,
    IQueryHandler<
        GetFactionsRankedBySimilarityQuery,
        IReadOnlyList<FactionsMatch>
    > getFactionsRankedBySimilarity,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<
        GetCreaturesRankedBySimilarityQuery,
        IReadOnlyList<CreaturesMatch>
    > getCreaturesRankedBySimilarity,
    IQueryHandler<GetCreatureCountByLocationIdsQuery, int> getCreatureCountByLocationIds
) : IQueryHandler<GetCreatureKnowledgeQuery, IReadOnlyList<LookupMatch>>
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
        candidates.AddRange(await FindCountryCandidates(query, cancellationToken));
        candidates.AddRange(await FindCityCandidates(query, cancellationToken));
        candidates.AddRange(await FindFactionCandidates(query, cancellationToken));
        candidates.AddRange(await FindCreatureCandidates(query, cancellationToken));

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

    private async Task<IReadOnlyList<Guid>> GetKnownSubjectIds(
        GetCreatureKnowledgeQuery query,
        KnowledgeSubjectType subjectType,
        CancellationToken cancellationToken
    ) =>
        await context
            .CreatureKnowledge.AsNoTracking()
            .Where(k =>
                k.KnowerId == query.AskingPerson.Id
                && k.SubjectType == subjectType
                && k.SubjectId != query.AskingPerson.Id
            )
            .Select(k => k.SubjectId)
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyList<Candidate>> FindCountryCandidates(
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await GetKnownSubjectIds(
            query,
            KnowledgeSubjectType.Country,
            cancellationToken
        );
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var matches = await getCountriesRankedBySimilarity.Handle(
            new GetCountriesRankedBySimilarityQuery
            {
                CandidateIds = candidateIds,
                SearchName = query.SubjectName,
                SimilarityThreshold = SimilarityThreshold,
                MaxMatches = MaxMatches,
            },
            cancellationToken
        );

        return matches
            .Select(m => new Candidate(m.Similarity, m.Name, KnowledgeSubjectType.Country, m.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<Candidate>> FindCityCandidates(
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await GetKnownSubjectIds(
            query,
            KnowledgeSubjectType.City,
            cancellationToken
        );
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var matches = await getCitiesRankedBySimilarity.Handle(
            new GetCitiesRankedBySimilarityQuery
            {
                CandidateIds = candidateIds,
                SearchName = query.SubjectName,
                SimilarityThreshold = SimilarityThreshold,
                MaxMatches = MaxMatches,
            },
            cancellationToken
        );

        return matches
            .Select(m => new Candidate(m.Similarity, m.Name, KnowledgeSubjectType.City, m.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<Candidate>> FindFactionCandidates(
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await GetKnownSubjectIds(
            query,
            KnowledgeSubjectType.Faction,
            cancellationToken
        );
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var matches = await getFactionsRankedBySimilarity.Handle(
            new GetFactionsRankedBySimilarityQuery
            {
                CandidateIds = candidateIds,
                SearchName = query.SubjectName,
                SimilarityThreshold = SimilarityThreshold,
                MaxMatches = MaxMatches,
            },
            cancellationToken
        );

        return matches
            .Select(m => new Candidate(m.Similarity, m.Name, KnowledgeSubjectType.Faction, m.Id))
            .ToArray();
    }

    private async Task<IReadOnlyList<Candidate>> FindCreatureCandidates(
        GetCreatureKnowledgeQuery query,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await GetKnownSubjectIds(
            query,
            KnowledgeSubjectType.Creature,
            cancellationToken
        );
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var matches = await getCreaturesRankedBySimilarity.Handle(
            new GetCreaturesRankedBySimilarityQuery
            {
                CandidateIds = candidateIds,
                SearchName = query.SubjectName,
                SimilarityThreshold = SimilarityThreshold,
                MaxMatches = MaxMatches,
            },
            cancellationToken
        );

        return matches
            .Select(m => new Candidate(m.Similarity, m.Name, KnowledgeSubjectType.Creature, m.Id))
            .ToArray();
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
                var country = await getCountryById.Handle(
                    new GetCountryByIdQuery { Id = candidate.EntityId },
                    cancellationToken
                );
                return country == null
                    ? null
                    : await BuildCountryResult(country, cancellationToken);
            case KnowledgeSubjectType.City:
                var city = await getCityById.Handle(
                    new GetCityByIdQuery { Id = candidate.EntityId },
                    cancellationToken
                );
                return city == null
                    ? null
                    : await BuildCityResult(city, query.WorldId, cancellationToken);
            case KnowledgeSubjectType.Faction:
                var faction = await getFactionById.Handle(
                    new GetFactionByIdQuery { Id = candidate.EntityId },
                    cancellationToken
                );
                return faction == null
                    ? null
                    : await BuildFactionResult(faction, cancellationToken);
            case KnowledgeSubjectType.Creature:
                var creature = await getCreatureById.Handle(
                    new GetCreatureByIdQuery { Id = candidate.EntityId },
                    cancellationToken
                );
                return creature == null
                    ? null
                    : await BuildPersonResult(creature, query.CurrentYear, cancellationToken);
            default:
                throw new ArgumentOutOfRangeException(nameof(candidate));
        }
    }

    private async Task<CountryLookupResult> BuildCountryResult(
        Country country,
        CancellationToken cancellationToken
    )
    {
        var capital = await getCapitalCityByCountryId.Handle(
            new GetCapitalCityByCountryIdQuery { CountryId = country.Id },
            cancellationToken
        );

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
        var state = await getStateById.Handle(
            new GetStateByIdQuery { Id = city.StateId },
            cancellationToken
        );
        var country =
            state != null
                ? await getCountryById.Handle(
                    new GetCountryByIdQuery { Id = state.CountryId },
                    cancellationToken
                )
                : null;
        var districts = await getDistrictsByCityId.Handle(
            new GetDistrictsByCityIdQuery { CityId = city.Id },
            cancellationToken
        );
        var locationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = city.Id },
            cancellationToken
        );
        var populationCount = await getCreatureCountByLocationIds.Handle(
            new GetCreatureCountByLocationIdsQuery { WorldId = worldId, LocationIds = locationIds },
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
        var leadership = await getFactionLeadership.Handle(
            new GetFactionLeadershipQuery { FactionId = faction.Id },
            cancellationToken
        );
        var leaderName = leadership.LeaderCreatureId is { } leaderCreatureId
            ? (
                await getCreatureById.Handle(
                    new GetCreatureByIdQuery { Id = leaderCreatureId },
                    cancellationToken
                )
            )?.Name
            : null;

        return new FactionLookupResult(
            faction.Name,
            faction.Description,
            leaderName,
            leadership.MemberCount
        );
    }

    private async Task<PersonLookupResult> BuildPersonResult(
        Creature creature,
        int currentYear,
        CancellationToken cancellationToken
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = creature.LocationId },
            cancellationToken
        );
        var state = location is null
            ? null
            : await getStateById.Handle(
                new GetStateByIdQuery { Id = location.StateId },
                cancellationToken
            );
        var city = location?.CityId is { } cityId
            ? await getCityById.Handle(new GetCityByIdQuery { Id = cityId }, cancellationToken)
            : null;
        var district = location?.DistrictId is { } districtId
            ? await getDistrictById.Handle(
                new GetDistrictByIdQuery { Id = districtId },
                cancellationToken
            )
            : null;
        var factionNames = await getNonCityFactionNamesByCreatureId.Handle(
            new GetNonCityFactionNamesByCreatureIdQuery { CreatureId = creature.Id },
            cancellationToken
        );

        var relativeSummaries = await getRelatives.Handle(
            new GetRelativesQuery { CreatureId = creature.Id },
            cancellationToken
        );
        var relatives = relativeSummaries
            .Select(r => new RelativeInfo(r.Name, r.RelationshipType.ToString()))
            .ToArray();

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
