using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.Abilities;
using TRPG.Application.Game;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class WorldGeneratorHouseholdTests
{
    private const int EpochYear = GameClock.EpochYear;
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly City _city = new()
    {
        WorldId = Guid.NewGuid(),
        StateId = Guid.NewGuid(),
        Name = "Test City",
    };
    private readonly District _residentialDistrict = new()
    {
        WorldId = Guid.NewGuid(),
        CityId = Guid.NewGuid(),
        Name = "Old Town",
        DistrictType = DistrictType.Residential,
    };
    private readonly WorldGeneratorInput _generatorInput = new()
    {
        Description = "test",
        MinHouseholdSize = 1,
        MaxHouseholdSize = 4,
    };
    private readonly WorldGenerator _worldGenerator = MakeWorldGenerator();

    private static WorldGenerator MakeWorldGenerator()
    {
        var abilityDefinitions = AbilityDefinitions.Create();
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(abilityDefinitions),
            new ArmorGenerator(abilityDefinitions),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        var creatureGenerator = new CreatureGenerator(
            itemGenerator,
            abilityDefinitions,
            new CreatureGeneratorSettings()
        );
        return new WorldGenerator(
            new BuildingGenerator(),
            null!,
            null!,
            creatureGenerator,
            NullLogger<WorldGenerator>.Instance
        );
    }

    private WorldGenerator.HouseholdGenerationContext MakeContext()
    {
        return new WorldGenerator.HouseholdGenerationContext
        {
            WorldId = _worldId,
            City = _city,
            StateId = _stateId,
            ResidentialDistrict = _residentialDistrict,
            DominantRace = CreatureType.Human,
            GeneratorInput = _generatorInput,
            UsedBuildingNames = [],
            CityFactionId = Guid.NewGuid(),
            CityBuildings = [],
            Creatures = [],
            FactionMembers = [],
            Items = [],
            InventoryItems = [],
            Skills = [],
            Abilities = [],
            Jobs = [],
            BuildingOwners = [],
            Rooms = [],
            Props = [],
            Relationships = [],
            RoomConnectorKeys = [],
            HouseholdByMemberId = [],
            HomeRoomIdByMemberId = [],
            FatherIds = [],
            EligibleForEmployment = [],
        };
    }

    [Fact]
    public void GenerateSingleHousehold_AlwaysProducesAnAdult()
    {
        for (var i = 0; i < 50; i++)
        {
            // Act
            var household = _worldGenerator.GenerateSingleHousehold(
                CreatureType.Human,
                _worldId,
                _stateId
            );

            // Assert
            var member = Assert.Single(household.Members);
            Assert.True(
                EpochYear - member.Creature.BirthYear >= 18,
                $"Sole householder was age {EpochYear - member.Creature.BirthYear}"
            );
        }
    }

    [Fact]
    public void GenerateFamilyHousehold_AlwaysProducesAdultParentsWithASpouseRelationship()
    {
        // Act
        var household = _worldGenerator.GenerateFamilyHousehold(
            CreatureType.Human,
            _worldId,
            _stateId,
            _generatorInput
        );

        // Assert
        var mother = household.Members[0];
        var father = household.Members[1];
        Assert.True(EpochYear - mother.Creature.BirthYear >= 18);
        Assert.True(EpochYear - father.Creature.BirthYear >= 18);
        Assert.Contains(
            household.Relationships,
            r =>
                r.SubjectId == mother.Creature.Id
                && r.RelativeId == father.Creature.Id
                && r.RelationshipType == RelationshipType.Husband
        );
        Assert.Contains(
            household.Relationships,
            r =>
                r.SubjectId == father.Creature.Id
                && r.RelativeId == mother.Creature.Id
                && r.RelationshipType == RelationshipType.Wife
        );
    }

    [Fact]
    public void GenerateAndRegisterHousehold_AssignsDesignatedProfession_AndRegistersFullSchedule()
    {
        // Arrange
        var context = MakeContext();

        // Act
        var worker = _worldGenerator.GenerateAndRegisterHousehold(context, Profession.Baker);

        // Assert
        Assert.NotNull(worker);
        Assert.Equal(Profession.Baker, worker.Profession);
        Assert.Contains(worker.Id, context.HomeRoomIdByMemberId.Keys);
        Assert.Contains(
            context.Jobs,
            j => j.CreatureId == worker.Id && j.Action == JobAction.Sleep
        );
        Assert.Contains(context.Jobs, j => j.CreatureId == worker.Id && j.Action == JobAction.Idle);
        Assert.Contains(context.Creatures, c => c.Id == worker.Id);
        Assert.DoesNotContain(worker, context.EligibleForEmployment);
    }

    [Fact]
    public void GenerateAndRegisterHousehold_GivesDesignatedWorkerASpouse_WhenHouseholdIsAFamily()
    {
        // Act — the family/single split is random per call, so retry until a family household comes up
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var context = MakeContext();
            var worker = _worldGenerator.GenerateAndRegisterHousehold(context, Profession.Baker)!;
            var household = context.HouseholdByMemberId[worker.Id];

            if (household.Count < 2)
            {
                continue;
            }

            // Assert
            Assert.Contains(
                context.Relationships,
                r =>
                    r.SubjectId == worker.Id
                    && (
                        r.RelationshipType == RelationshipType.Husband
                        || r.RelationshipType == RelationshipType.Wife
                    )
            );
            return;
        }

        Assert.Fail(
            "Never generated a family household in 30 attempts — FamilyUnitChance may have regressed."
        );
    }

    [Fact]
    public void GenerateAndRegisterHousehold_NeverAssignsARealJob_WhenNoProfessionIsDesignated()
    {
        // Arrange
        var context = MakeContext();

        // Act
        var worker = _worldGenerator.GenerateAndRegisterHousehold(context, null);

        // Assert — with no designated worker, a member is either Unemployed (eligible for hire later),
        // a Homemaker (a mother with kids), or a minor (null profession) — never an actual job
        Assert.Null(worker);
        Assert.All(
            context.Creatures,
            c =>
                Assert.True(
                    c.Profession is null or Profession.Unemployed or Profession.Homemaker,
                    $"Unexpected profession {c.Profession} with no designated worker"
                )
        );
        var expectedEligible = context.Creatures.Count(c => c.Profession == Profession.Unemployed);
        Assert.Equal(expectedEligible, context.EligibleForEmployment.Count);
    }

    [Fact]
    public void GenerateAndRegisterHousehold_NeverMakesTheSoleHouseholderAMinor()
    {
        for (var i = 0; i < 30; i++)
        {
            // Arrange
            var context = MakeContext();

            // Act
            _worldGenerator.GenerateAndRegisterHousehold(context, null);

            // Assert
            if (context.Creatures.Count != 1)
            {
                continue;
            }

            var soleResident = context.Creatures[0];
            Assert.True(
                EpochYear - soleResident.BirthYear >= 18,
                $"Sole householder was age {EpochYear - soleResident.BirthYear}"
            );
        }
    }
}
