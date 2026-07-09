using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Services;

internal record KnowledgeQuery(
    Guid WorldId,
    string SubjectName,
    int CurrentYear,
    Creature AskingPerson
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

internal class CreatureKnowledgeService(TrpgDbContext context)
{
    // Professions whose work naturally reaches beyond their home territory — diplomacy, trade,
    // scholarship, and arcane study — versus every other profession, which stays local.
    private static readonly HashSet<Profession> WorldlyProfessions =
    [
        Profession.Merchant,
        Profession.Politician,
        Profession.Scholar,
        Profession.Mage,
    ];

    public async Task<LookupResult?> GetInfo(
        KnowledgeQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var askingPerson = query.AskingPerson;
        var isWorldly =
            askingPerson.Profession is { } profession && WorldlyProfessions.Contains(profession);
        var askingPersonState = await context.States.FindAsync(
            [askingPerson.StateId],
            cancellationToken
        );

        var country = await context
            .Countries.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.WorldId == query.WorldId && c.Name == query.SubjectName,
                cancellationToken
            );
        if (country != null)
        {
            var knowsCountry = isWorldly || country.Id == askingPersonState?.CountryId;
            return knowsCountry ? await BuildCountryResult(country, cancellationToken) : null;
        }

        var city = await context
            .Cities.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.WorldId == query.WorldId && c.Name == query.SubjectName,
                cancellationToken
            );
        if (city != null)
        {
            var knowsCity = isWorldly || city.StateId == askingPerson.StateId;
            return knowsCity ? await BuildCityResult(city, query.WorldId, cancellationToken) : null;
        }

        var faction = await context
            .Factions.AsNoTracking()
            .FirstOrDefaultAsync(
                f => f.WorldId == query.WorldId && f.Name == query.SubjectName,
                cancellationToken
            );
        if (faction != null)
        {
            var isMember = await context.FactionMembers.AnyAsync(
                fm => fm.CreatureId == askingPerson.Id && fm.FactionId == faction.Id,
                cancellationToken
            );
            var knowsFaction = isWorldly || isMember;
            return knowsFaction ? await BuildFactionResult(faction, cancellationToken) : null;
        }

        // Names aren't guaranteed unique world-wide, so a name can match several creatures — search
        // among all of them for the one the asker actually knows, rather than an arbitrary first match.
        var candidates = await context
            .Creatures.AsNoTracking()
            .Where(p => p.WorldId == query.WorldId && p.Name == query.SubjectName)
            .ToArrayAsync(cancellationToken);

        foreach (var candidate in candidates)
        {
            if (await KnowsPerson(askingPerson, candidate, cancellationToken))
            {
                return await BuildPersonResult(candidate, query.CurrentYear, cancellationToken);
            }
        }

        return null;
    }

    // Personal knowledge of a specific individual never scales with profession or intelligence —
    // it comes only from a direct connection (family, faction, or living in the same city).
    private async Task<bool> KnowsPerson(
        Creature askingPerson,
        Creature subject,
        CancellationToken cancellationToken
    )
    {
        if (askingPerson.Id == subject.Id)
        {
            return true;
        }

        if (askingPerson.CityId != null && askingPerson.CityId == subject.CityId)
        {
            return true;
        }

        var isRelated = await context.Relationships.AnyAsync(
            r => r.SubjectId == askingPerson.Id && r.RelativeId == subject.Id,
            cancellationToken
        );
        if (isRelated)
        {
            return true;
        }

        return await (
            from fm1 in context.FactionMembers
            where fm1.CreatureId == askingPerson.Id
            join fm2 in context.FactionMembers on fm1.FactionId equals fm2.FactionId
            where fm2.CreatureId == subject.Id
            select fm1
        ).AnyAsync(cancellationToken);
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
        var state = await context.States.FindAsync([city.StateId], cancellationToken);
        var country =
            state != null
                ? await context.Countries.FindAsync([state.CountryId], cancellationToken)
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
