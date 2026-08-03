using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class KnowledgeGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();

    private KnowledgeGeneratorInput MakeInput(
        IReadOnlyList<Creature>? creatures = null,
        IReadOnlyList<Relationship>? relationships = null,
        IReadOnlyList<FactionMember>? factionMembers = null,
        IReadOnlyList<Faction>? factions = null,
        IReadOnlyList<City>? cities = null,
        IReadOnlyList<Location>? locations = null,
        IReadOnlyList<State>? states = null,
        IReadOnlyList<Country>? countries = null
    )
    {
        return new KnowledgeGeneratorInput
        {
            WorldId = _worldId,
            Creatures = creatures ?? [],
            Relationships = relationships ?? [],
            FactionMembers = factionMembers ?? [],
            Factions = factions ?? [],
            Cities = cities ?? [],
            Locations = locations ?? [],
            States = states ?? [],
            Countries = countries ?? [],
        };
    }

    private Location MakeCityLocation(Guid cityId) =>
        Builders.MakeLocation(_worldId, cityId: cityId);

    [Fact]
    public void Generate_AddsSelfKnowledge_ForEveryCreature()
    {
        // Arrange
        var creature = Builders.MakeCreature(_worldId);

        // Act
        var knowledge = KnowledgeGenerator.Generate(MakeInput(creatures: [creature]));

        // Assert
        Assert.Contains(
            knowledge,
            k =>
                k.KnowerId == creature.Id
                && k.SubjectId == creature.Id
                && k.SubjectType == KnowledgeSubjectType.Creature
        );
    }

    [Fact]
    public void Generate_AddsMutualKnowledge_WhenCreaturesShareACity()
    {
        // Arrange
        var cityLocation = MakeCityLocation(Guid.NewGuid());
        var outsiderLocation = MakeCityLocation(Guid.NewGuid());
        var resident1 = Builders.MakeCreature(_worldId, locationId: cityLocation.Id);
        var resident2 = Builders.MakeCreature(_worldId, locationId: cityLocation.Id);
        var outsider = Builders.MakeCreature(_worldId, locationId: outsiderLocation.Id);

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(
                creatures: [resident1, resident2, outsider],
                locations: [cityLocation, outsiderLocation]
            )
        );

        // Assert
        Assert.Contains(knowledge, k => k.KnowerId == resident1.Id && k.SubjectId == resident2.Id);
        Assert.Contains(knowledge, k => k.KnowerId == resident2.Id && k.SubjectId == resident1.Id);
        Assert.DoesNotContain(
            knowledge,
            k => k.KnowerId == resident1.Id && k.SubjectId == outsider.Id
        );
        Assert.DoesNotContain(
            knowledge,
            k => k.KnowerId == outsider.Id && k.SubjectId == resident1.Id
        );
    }

    [Fact]
    public void Generate_AddsKnowledge_WhenCreaturesAreRelated()
    {
        // Arrange
        var parent = Builders.MakeCreature(_worldId);
        var child = Builders.MakeCreature(_worldId);
        var relationship = new Relationship
        {
            SubjectId = child.Id,
            RelativeId = parent.Id,
            RelationshipType = RelationshipType.Mother,
            WorldId = _worldId,
        };

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(creatures: [parent, child], relationships: [relationship])
        );

        // Assert
        Assert.Contains(
            knowledge,
            k =>
                k.KnowerId == child.Id
                && k.SubjectId == parent.Id
                && k.SubjectType == KnowledgeSubjectType.Creature
        );
    }

    [Fact]
    public void Generate_AddsFactionAndFellowMemberKnowledge_WhenCreaturesShareAFaction()
    {
        // Arrange
        var member1 = Builders.MakeCreature(_worldId);
        var member2 = Builders.MakeCreature(_worldId);
        var faction = new Faction
        {
            WorldId = _worldId,
            Name = $"Faction-{Guid.NewGuid():N}",
            Description = "A test faction",
        };
        FactionMember[] members =
        [
            new()
            {
                FactionId = faction.Id,
                CreatureId = member1.Id,
                Role = FactionRole.Member,
                WorldId = _worldId,
            },
            new()
            {
                FactionId = faction.Id,
                CreatureId = member2.Id,
                Role = FactionRole.Member,
                WorldId = _worldId,
            },
        ];

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(creatures: [member1, member2], factionMembers: members, factions: [faction])
        );

        // Assert
        Assert.Contains(
            knowledge,
            k =>
                k.KnowerId == member1.Id
                && k.SubjectId == faction.Id
                && k.SubjectType == KnowledgeSubjectType.Faction
        );
        Assert.Contains(knowledge, k => k.KnowerId == member1.Id && k.SubjectId == member2.Id);
        Assert.Contains(knowledge, k => k.KnowerId == member2.Id && k.SubjectId == member1.Id);
    }

    [Fact]
    public void Generate_AddsHomeCountryAndStateCities_ForEveryCreature()
    {
        // Arrange
        var world = Builders.MakeWorld();
        var homeCountry = Builders.MakeCountry(_worldId);
        var otherCountry = Builders.MakeCountry(_worldId);
        var homeState = Builders.MakeState(homeCountry.Id, _worldId);
        var otherState = Builders.MakeState(otherCountry.Id, _worldId);
        var homeCity = Builders.MakeCity(homeState.Id, homeCountry.Id, worldId: _worldId);
        var otherCity = Builders.MakeCity(otherState.Id, otherCountry.Id, worldId: _worldId);
        var creature = Builders.MakeCreature(_worldId, stateId: homeState.Id);

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(
                creatures: [creature],
                cities: [homeCity, otherCity],
                states: [homeState, otherState],
                countries: [homeCountry, otherCountry]
            )
        );

        // Assert
        Assert.Contains(
            knowledge,
            k =>
                k.KnowerId == creature.Id
                && k.SubjectId == homeCountry.Id
                && k.SubjectType == KnowledgeSubjectType.Country
        );
        Assert.Contains(
            knowledge,
            k =>
                k.KnowerId == creature.Id
                && k.SubjectId == homeCity.Id
                && k.SubjectType == KnowledgeSubjectType.City
        );
        Assert.DoesNotContain(knowledge, k => k.SubjectId == otherCountry.Id);
        Assert.DoesNotContain(knowledge, k => k.SubjectId == otherCity.Id);
    }

    [Fact]
    public void Generate_AddsAllCountriesCitiesAndFactions_WhenProfessionIsWorldly()
    {
        // Arrange
        var country = Builders.MakeCountry(_worldId);
        var farCountry = Builders.MakeCountry(_worldId);
        var state = Builders.MakeState(country.Id, _worldId);
        var farState = Builders.MakeState(farCountry.Id, _worldId);
        var farCity = Builders.MakeCity(farState.Id, farCountry.Id, worldId: _worldId);
        var faction = new Faction
        {
            WorldId = _worldId,
            Name = $"Faction-{Guid.NewGuid():N}",
            Description = "A test faction",
        };
        var merchant = Builders.MakeCreature(
            _worldId,
            profession: Profession.Merchant,
            stateId: state.Id
        );

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(
                creatures: [merchant],
                factions: [faction],
                cities: [farCity],
                states: [state, farState],
                countries: [country, farCountry]
            )
        );

        // Assert
        Assert.Contains(knowledge, k => k.KnowerId == merchant.Id && k.SubjectId == farCountry.Id);
        Assert.Contains(knowledge, k => k.KnowerId == merchant.Id && k.SubjectId == farCity.Id);
        Assert.Contains(knowledge, k => k.KnowerId == merchant.Id && k.SubjectId == faction.Id);
    }

    [Fact]
    public void Generate_EmitsNoDuplicates_WhenRulesOverlap()
    {
        // Arrange — same city AND related AND same faction: every pair rule fires for this couple
        var cityLocation = MakeCityLocation(Guid.NewGuid());
        var wife = Builders.MakeCreature(_worldId, locationId: cityLocation.Id);
        var husband = Builders.MakeCreature(_worldId, locationId: cityLocation.Id);
        var faction = new Faction
        {
            WorldId = _worldId,
            Name = $"Faction-{Guid.NewGuid():N}",
            Description = "A test faction",
        };
        FactionMember[] members =
        [
            new()
            {
                FactionId = faction.Id,
                CreatureId = wife.Id,
                Role = FactionRole.Member,
                WorldId = _worldId,
            },
            new()
            {
                FactionId = faction.Id,
                CreatureId = husband.Id,
                Role = FactionRole.Member,
                WorldId = _worldId,
            },
        ];
        var relationship = new Relationship
        {
            SubjectId = wife.Id,
            RelativeId = husband.Id,
            RelationshipType = RelationshipType.Husband,
            WorldId = _worldId,
        };

        // Act
        var knowledge = KnowledgeGenerator.Generate(
            MakeInput(
                creatures: [wife, husband],
                relationships: [relationship],
                factionMembers: members,
                factions: [faction],
                locations: [cityLocation]
            )
        );

        // Assert
        Assert.Single(knowledge, k => k.KnowerId == wife.Id && k.SubjectId == husband.Id);
    }
}
