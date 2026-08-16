using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal class KnowledgeGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Location> Locations { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
}

internal static class KnowledgeGenerator
{
    internal static readonly HashSet<Profession> WorldlyProfessions =
    [
        Profession.Merchant,
        Profession.Politician,
        Profession.Scholar,
        Profession.Mage,
    ];

    public static IReadOnlyList<CreatureKnowledge> Generate(KnowledgeGeneratorInput input)
    {
        var seen = new HashSet<(Guid KnowerId, Guid SubjectId)>();
        var knowledge = new List<CreatureKnowledge>();

        void Add(Guid knowerId, Guid subjectId, KnowledgeSubjectType subjectType)
        {
            if (seen.Add((knowerId, subjectId)))
            {
                knowledge.Add(
                    new CreatureKnowledge
                    {
                        KnowerId = knowerId,
                        SubjectId = subjectId,
                        SubjectType = subjectType,
                        WorldId = input.WorldId,
                    }
                );
            }
        }

        var countryIdByStateId = input.States.ToDictionary(s => s.Id, s => s.CountryId);
        var cityIdsByStateId = input
            .Cities.GroupBy(c => c.StateId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Id).ToList());
        var locationsById = input.Locations.ToDictionary(l => l.Id);

        foreach (var creature in input.Creatures)
        {
            Add(creature.Id, creature.Id, KnowledgeSubjectType.Creature);

            if (!locationsById.TryGetValue(creature.LocationId, out var creatureLocation))
            {
                continue;
            }

            if (countryIdByStateId.TryGetValue(creatureLocation.StateId, out var countryId))
            {
                Add(creature.Id, countryId, KnowledgeSubjectType.Country);
            }

            if (cityIdsByStateId.TryGetValue(creatureLocation.StateId, out var stateCityIds))
            {
                foreach (var cityId in stateCityIds)
                {
                    Add(creature.Id, cityId, KnowledgeSubjectType.City);
                }
            }

            if (creature.Profession is { } profession && WorldlyProfessions.Contains(profession))
            {
                foreach (var country in input.Countries)
                {
                    Add(creature.Id, country.Id, KnowledgeSubjectType.Country);
                }

                foreach (var city in input.Cities)
                {
                    Add(creature.Id, city.Id, KnowledgeSubjectType.City);
                }

                foreach (var faction in input.Factions)
                {
                    Add(creature.Id, faction.Id, KnowledgeSubjectType.Faction);
                }
            }
        }

        var creatureCityIds = input.Creatures.ToDictionary(
            c => c.Id,
            c => locationsById.TryGetValue(c.LocationId, out var l) ? l.CityId : null
        );

        foreach (
            var cityResidents in input
                .Creatures.Where(c => creatureCityIds[c.Id] != null)
                .GroupBy(c => creatureCityIds[c.Id])
        )
        {
            var residents = cityResidents.ToList();
            foreach (var knower in residents)
            {
                foreach (var subject in residents)
                {
                    Add(knower.Id, subject.Id, KnowledgeSubjectType.Creature);
                }
            }
        }

        foreach (var relationship in input.Relationships)
        {
            Add(relationship.SubjectId, relationship.RelativeId, KnowledgeSubjectType.Creature);
        }

        foreach (var factionMembers in input.FactionMembers.GroupBy(fm => fm.FactionId))
        {
            var members = factionMembers.ToList();
            foreach (var member in members)
            {
                Add(member.CreatureId, factionMembers.Key, KnowledgeSubjectType.Faction);

                foreach (var other in members)
                {
                    Add(member.CreatureId, other.CreatureId, KnowledgeSubjectType.Creature);
                }
            }
        }

        return knowledge;
    }
}
