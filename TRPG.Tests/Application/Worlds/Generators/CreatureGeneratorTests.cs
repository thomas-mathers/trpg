using TRPG.Application.Abilities;
using TRPG.Application.Configuration;
using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class CreatureGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly CreatureGenerator _creatureGenerator = MakeCreatureGenerator();

    private static CreatureGenerator MakeCreatureGenerator(CreatureGeneratorOptions? options = null)
    {
        var abilityDefinitions = AbilityDefinitions.Create();
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(abilityDefinitions),
            new ArmorGenerator(abilityDefinitions),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        return new CreatureGenerator(
            itemGenerator,
            abilityDefinitions,
            new TestOptionsSnapshot<CreatureGeneratorOptions>(
                options ?? new CreatureGeneratorOptions()
            ),
            Builders.MakeStatFormulas(options)
        );
    }

    private CreatureGeneratorInput MakeInput(
        Profession profession,
        int level = 0,
        IReadOnlyDictionary<AttributeName, int>? startingAttributeAllocation = null
    )
    {
        return new CreatureGeneratorInput(
            CreatureType.Human,
            profession,
            _worldId,
            _stateId,
            _stateId,
            Level: level,
            StartingAttributeAllocation: startingAttributeAllocation
        );
    }

    [Fact]
    public void Generate_KeepsLevelAtMostTwenty_WhenProfessionIsCivilian()
    {
        for (var i = 0; i < 100; i++)
        {
            // Act
            var result = _creatureGenerator.Generate(MakeInput(Profession.Baker));

            // Assert
            Assert.InRange(result.Creature.Level, 1, 20);
        }
    }

    [Fact]
    public void Generate_KeepsLevelAtLeastFive_WhenProfessionIsCombat()
    {
        for (var i = 0; i < 100; i++)
        {
            // Act
            var result = _creatureGenerator.Generate(MakeInput(Profession.Mercenary));

            // Assert
            Assert.InRange(result.Creature.Level, 5, 100);
        }
    }

    [Fact]
    public void Generate_UsesExplicitLevel_WhenOneIsProvided()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 1));

        // Assert
        Assert.Equal(1, result.Creature.Level);
    }

    [Fact]
    public void Generate_StartsCreatureAtFullResources()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 1));

        // Assert
        Assert.Equal(result.Creature.MaximumHp, result.Creature.CurrentHp);
        Assert.Equal(result.Creature.MaximumAp, result.Creature.CurrentAp);
        Assert.Equal(result.Creature.MaximumMp, result.Creature.CurrentMp);
        Assert.Equal(TimeSpan.Zero, result.Creature.LastRegenPlaytime);
    }

    [Fact]
    public void Generate_AppliesAllocationOnTopOfBaseline_WhenStartingAttributeAllocationProvided()
    {
        // Arrange — default options: BaseAttributes all 1, PointsPerLevel 5, so level 1 grants
        // exactly 5 points; spending all 5 on Strength should leave every other stat at baseline.
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Strength] = 5,
            }
        );

        // Act
        var result = _creatureGenerator.Generate(input);

        // Assert
        Assert.Equal(6, result.Creature.BaseAttributes.Strength);
        Assert.Equal(1, result.Creature.BaseAttributes.Defense);
        Assert.Equal(1, result.Creature.BaseAttributes.Dexterity);
        Assert.Equal(1, result.Creature.BaseAttributes.Endurance);
        Assert.Equal(1, result.Creature.BaseAttributes.Stamina);
        Assert.Equal(1, result.Creature.BaseAttributes.Mana);
        Assert.Equal(1, result.Creature.BaseAttributes.Intelligence);
    }

    [Fact]
    public void Generate_AllowsPartialAllocation_LeavingRemainderAtBaseline()
    {
        // Arrange — only spend 2 of the 5 available points
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Mana] = 2,
            }
        );

        // Act
        var result = _creatureGenerator.Generate(input);

        // Assert
        Assert.Equal(3, result.Creature.BaseAttributes.Mana);
        Assert.Equal(1, result.Creature.BaseAttributes.Strength);
    }

    [Fact]
    public void Generate_Throws_WhenAllocationExceedsAvailablePoints()
    {
        // Arrange — level 1 only grants 5 points
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Strength] = 6,
            }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_AppliesNegativeDelta_LoweringAnAttributeBelowItsBase()
    {
        // Arrange — base Endurance of 3 lets a -1 delta stay at or above the floor of 1
        var generator = MakeCreatureGenerator(
            new CreatureGeneratorOptions
            {
                PointsPerLevel = 5,
                BaseAttributes = new StartingAttributes { Endurance = 3 },
            }
        );
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Endurance] = -1,
            }
        );

        // Act
        var result = generator.Generate(input);

        // Assert
        Assert.Equal(2, result.Creature.BaseAttributes.Endurance);
    }

    [Fact]
    public void Generate_Throws_WhenAllocationWouldTakeAnAttributeBelowOne()
    {
        // Arrange — Strength starts at the default base of 1
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Strength] = -1,
            }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_AllowsNetReallocation_WhenLoweringOneAttributeFundsAnother()
    {
        // Arrange — base Endurance of 3, 5 points available; the positive-only sum of these
        // deltas (6) would exceed 5, but the net sum (5) does not
        var generator = MakeCreatureGenerator(
            new CreatureGeneratorOptions
            {
                PointsPerLevel = 5,
                BaseAttributes = new StartingAttributes { Endurance = 3 },
            }
        );
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Endurance] = -1,
                [AttributeName.Strength] = 6,
            }
        );

        // Act
        var result = generator.Generate(input);

        // Assert
        Assert.Equal(7, result.Creature.BaseAttributes.Strength);
        Assert.Equal(2, result.Creature.BaseAttributes.Endurance);
    }

    [Fact]
    public void Generate_Throws_WhenAllocationTargetsDefense()
    {
        // Arrange — Defense is not player-allocatable
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int>
            {
                [AttributeName.Defense] = 1,
            }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_NeverRaisesAZeroWeightStatAboveOne_WhenNoStartingAttributeAllocationIsProvided()
    {
        // Act — Mage has zero Strength affinity, so it can never win the weighted lottery
        var result = _creatureGenerator.Generate(MakeInput(Profession.Mage, level: 20));

        // Assert
        Assert.Equal(1, result.Creature.BaseAttributes.Strength);
    }

    [Fact]
    public void Generate_TotalAttributePoints_MatchesBaseAttributesPlusLevelBonus_WhenNoStartingAttributeAllocationIsProvided()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 10));

        // Assert — default BaseAttributes total 7, PointsPerLevel 5, level 10
        var attributes = result.Creature.BaseAttributes;
        var total =
            attributes.Strength
            + attributes.Defense
            + attributes.Dexterity
            + attributes.Endurance
            + attributes.Stamina
            + attributes.Mana
            + attributes.Intelligence;
        Assert.Equal(7 + 10 * 5, total);
    }

    [Fact]
    public void Generate_GivesEveryCreature_LevelOneInEverySkill()
    {
        // Act — Baker has no profession skills at all
        var result = _creatureGenerator.Generate(MakeInput(Profession.Baker, level: 5));

        // Assert
        Assert.Equal(Enum.GetValues<Skill>().Length, result.Skills.Count);
        Assert.All(result.Skills, skill => Assert.Equal(1, skill.Level));
    }

    [Fact]
    public void Generate_ScalesOnlyProfessionSkills_ToCharacterLevel()
    {
        // Act — Knight's profession skills are Swordsmanship and Warfare
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 10));

        // Assert
        var skillLevels = result.Skills.ToDictionary(s => s.Skill, s => s.Level);
        Assert.Equal(10, skillLevels[Skill.Swordsmanship]);
        Assert.Equal(10, skillLevels[Skill.Warfare]);
        Assert.Equal(1, skillLevels[Skill.Archery]);
    }

    [Fact]
    public void Generate_GrantsLevelOneAbilities_AcrossEverySkillTree_RegardlessOfProfession()
    {
        // Act — Baker has no Swordsmanship profession skill, but "Slash" unlocks at skill level 1
        var result = _creatureGenerator.Generate(MakeInput(Profession.Baker, level: 1));

        // Assert
        Assert.Contains(result.Abilities, a => a.AbilityName == "Slash");
    }
}
