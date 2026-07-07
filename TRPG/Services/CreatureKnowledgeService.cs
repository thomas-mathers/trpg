using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal record KnowledgeQuery(Guid WorldId, string SubjectName, int CurrentYear);

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

internal sealed record PersonLookupResult(
    string Name,
    string CreatureType,
    string Gender,
    string? Profession,
    int Level,
    int Age,
    string Biography,
    IReadOnlyCollection<string> FactionNames,
    string? StateName,
    string? CityName,
    string? DistrictName
) : LookupResult;

internal class CreatureKnowledgeService(TrpgDbContext context) {
    public async Task<LookupResult?> GetInfo(KnowledgeQuery query, CancellationToken cancellationToken = default) {
        var country = await context.Countries.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WorldId == query.WorldId && c.Name == query.SubjectName, cancellationToken);
        if (country != null) {
            return await BuildCountryResult(country, cancellationToken);
        }

        var city = await context.Cities.AsNoTracking()
            .FirstOrDefaultAsync(c => c.WorldId == query.WorldId && c.Name == query.SubjectName, cancellationToken);
        if (city != null) {
            return await BuildCityResult(city, query.WorldId, cancellationToken);
        }

        var faction = await context.Factions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.WorldId == query.WorldId && f.Name == query.SubjectName, cancellationToken);
        if (faction != null) {
            return await BuildFactionResult(faction, cancellationToken);
        }

        var person = await context.Creatures.AsNoTracking()
            .FirstOrDefaultAsync(p => p.WorldId == query.WorldId && p.Name == query.SubjectName, cancellationToken);
        return person != null ? await BuildPersonResult(person, query.CurrentYear, cancellationToken) : null;
    }

    private async Task<CountryLookupResult> BuildCountryResult(Country country,
        CancellationToken cancellationToken) {
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

    private async Task<CityLookupResult> BuildCityResult(City city, Guid worldId,
        CancellationToken cancellationToken) {
        var state = await context.States.FindAsync([city.StateId], cancellationToken);
        var country = state != null ? await context.Countries.FindAsync([state.CountryId], cancellationToken) : null;
        var districts = await context.Districts.AsNoTracking()
            .Where(d => d.CityId == city.Id)
            .ToArrayAsync(cancellationToken);
        var populationCount =
            await context.Creatures.CountAsync(p => p.WorldId == worldId && p.CityId == city.Id, cancellationToken);

        return new CityLookupResult(
            city.Name,
            city.Description,
            city.IsCapital,
            state?.Name,
            country?.Name,
            districts.Select(d => new CityDistrictInfo(d.Name, d.DistrictType.ToString())).ToArray(),
            populationCount
        );
    }

    private async Task<FactionLookupResult> BuildFactionResult(Faction faction,
        CancellationToken cancellationToken) {
        var leaderName = await (
            from fm in context.FactionMembers
            where fm.FactionId == faction.Id && fm.Role == FactionRole.Leader
            join c in context.Creatures on fm.CreatureId equals c.Id
            select c.Name
        ).FirstOrDefaultAsync(cancellationToken);
        var memberCount =
            await context.FactionMembers.CountAsync(fm => fm.FactionId == faction.Id, cancellationToken);

        return new FactionLookupResult(faction.Name, faction.Description, leaderName, memberCount);
    }

    private async Task<PersonLookupResult> BuildPersonResult(Creature creature, int currentYear,
        CancellationToken cancellationToken) {
        var state = await context.States.FindAsync([creature.StateId], cancellationToken);
        var city = creature.CityId is { } cityId
            ? await context.Cities.FindAsync([cityId], cancellationToken)
            : null;
        var district = creature.DistrictId is { } districtId
            ? await context.Districts.FindAsync([districtId], cancellationToken)
            : null;
        var factionNames = await (
            from fm in context.FactionMembers
            where fm.CreatureId == creature.Id
            join f in context.Factions on fm.FactionId equals f.Id
            select f.Name
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
            state?.Name,
            city?.Name,
            district?.Name
        );
    }
}
