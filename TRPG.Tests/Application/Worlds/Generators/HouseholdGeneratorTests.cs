using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.GameSessions;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class HouseholdGeneratorTests
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
    private readonly HouseholdGenerator _householdGenerator = MakeHouseholdGenerator();

    private static HouseholdGenerator MakeHouseholdGenerator()
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
            new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions())
        );
        return new HouseholdGenerator(new BuildingGenerator(), creatureGenerator);
    }

    private HouseholdGeneratorInput MakeInput()
    {
        return new HouseholdGeneratorInput
        {
            WorldId = _worldId,
            City = _city,
            StateId = _stateId,
            ResidentialDistrict = _residentialDistrict,
            DominantRace = CreatureType.Human,
            GeneratorInput = _generatorInput,
            UsedBuildingNames = [],
        };
    }

    [Fact]
    public void GenerateSingleHousehold_AlwaysProducesAnAdult()
    {
        for (var i = 0; i < 50; i++)
        {
            // Act
            var household = _householdGenerator.GenerateSingleHousehold(
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
        var household = _householdGenerator.GenerateFamilyHousehold(
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
    public void Generate_AssignsDesignatedProfession_AndProducesFullSchedule()
    {
        // Act
        var household = _householdGenerator.Generate(MakeInput(), Profession.Baker);

        // Assert
        var worker = household.DesignatedWorker;
        Assert.NotNull(worker);
        Assert.Equal(Profession.Baker, worker.Profession);
        Assert.Contains(
            household.Jobs,
            j => j.CreatureId == worker.Id && j.Action == CreatureJobAction.Sleep
        );
        Assert.Contains(
            household.Jobs,
            j => j.CreatureId == worker.Id && j.Action == CreatureJobAction.Idle
        );
        Assert.Contains(household.Members, m => m.Creature.Id == worker.Id);
        Assert.DoesNotContain(worker, household.EligibleForEmployment);
    }

    [Fact]
    public void Generate_GivesDesignatedWorkerASpouse_WhenHouseholdIsAFamily()
    {
        // Act — the family/single split is random per call, so retry until a family household comes up
        for (var attempt = 0; attempt < 30; attempt++)
        {
            var household = _householdGenerator.Generate(MakeInput(), Profession.Baker);
            var worker = household.DesignatedWorker!;

            if (household.Members.Count < 2)
            {
                continue;
            }

            // Assert
            Assert.Contains(
                household.Relationships,
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
    public void Generate_NeverAssignsARealJob_WhenNoProfessionIsDesignated()
    {
        // Act
        var household = _householdGenerator.Generate(MakeInput(), null);

        // Assert — with no designated worker, a member is either Unemployed (eligible for hire later),
        // a Homemaker (a mother with kids), or a minor (null profession) — never an actual job
        Assert.Null(household.DesignatedWorker);
        Assert.All(
            household.Members,
            m =>
                Assert.True(
                    m.Creature.Profession is null or Profession.Unemployed or Profession.Homemaker,
                    $"Unexpected profession {m.Creature.Profession} with no designated worker"
                )
        );
        var expectedEligible = household.Members.Count(m =>
            m.Creature.Profession == Profession.Unemployed
        );
        Assert.Equal(expectedEligible, household.EligibleForEmployment.Count);
    }

    [Fact]
    public void Generate_NeverMakesTheSoleHouseholderAMinor()
    {
        for (var i = 0; i < 30; i++)
        {
            // Act
            var household = _householdGenerator.Generate(MakeInput(), null);

            // Assert
            if (household.Members.Count != 1)
            {
                continue;
            }

            var soleResident = household.Members[0].Creature;
            Assert.True(
                EpochYear - soleResident.BirthYear >= 18,
                $"Sole householder was age {EpochYear - soleResident.BirthYear}"
            );
        }
    }
}
