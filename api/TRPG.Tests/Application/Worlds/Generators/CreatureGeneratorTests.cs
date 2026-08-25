using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class CreatureGeneratorTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();
    private readonly CreatureGenerator _creatureGenerator = Builders.MakeCreatureGenerator();

    private CreatureGeneratorInput MakeInput(
        Profession profession,
        int level = 1,
        IReadOnlyDictionary<AllocatableAttributeName, int>? startingAttributeAllocation = null
    )
    {
        return new CreatureGeneratorInput(
            CreatureType.Human,
            CreatureArchetype.For(profession),
            _worldId,
            _locationId,
            MinLevel: level,
            MaxLevel: level,
            StartingAttributeAllocation: startingAttributeAllocation
        );
    }

    private CreatureGeneratorInput MakeMonsterInput(CreatureArchetype archetype, int level)
    {
        return new CreatureGeneratorInput(
            CreatureType.Human,
            archetype,
            _worldId,
            _locationId,
            MinLevel: level,
            MaxLevel: level
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
        // Arrange — default options: BaseAttributes all 5, PointsPerLevel 5, so level 1 grants
        // exactly 5 points; spending all 5 on Strength should leave every other stat at baseline.
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Strength] = 5,
            }
        );

        // Act
        var result = _creatureGenerator.Generate(input);

        // Assert
        Assert.Equal(10, result.Creature.BaseAttributes.Strength);
        Assert.Equal(5, result.Creature.BaseAttributes.Defense);
        Assert.Equal(5, result.Creature.BaseAttributes.Dexterity);
        Assert.Equal(5, result.Creature.BaseAttributes.Endurance);
        Assert.Equal(5, result.Creature.BaseAttributes.Stamina);
        Assert.Equal(5, result.Creature.BaseAttributes.Mana);
        Assert.Equal(5, result.Creature.BaseAttributes.Intelligence);
    }

    [Fact]
    public void Generate_AllowsPartialAllocation_LeavingRemainderAtBaseline()
    {
        // Arrange — only spend 2 of the 5 available points
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Mana] = 2,
            }
        );

        // Act
        var result = _creatureGenerator.Generate(input);

        // Assert
        Assert.Equal(7, result.Creature.BaseAttributes.Mana);
        Assert.Equal(5, result.Creature.BaseAttributes.Strength);
    }

    [Fact]
    public void Generate_Throws_WhenAllocationExceedsAvailablePoints()
    {
        // Arrange — level 1 only grants 5 points
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Strength] = 6,
            }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_AppliesNegativeDelta_LoweringAnAttributeBelowItsBase()
    {
        // Arrange — base Endurance of 3 lets a -1 delta stay at or above the floor of 1
        var generator = Builders.MakeCreatureGenerator(
            new CreatureGeneratorOptions
            {
                PointsPerLevel = 5,
                BaseAttributes = new StartingAttributes { Endurance = 3 },
            }
        );
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Endurance] = -1,
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
        // Arrange — Strength pinned to a base of 1 so a -1 delta would take it to 0
        var generator = Builders.MakeCreatureGenerator(
            new CreatureGeneratorOptions
            {
                PointsPerLevel = 5,
                BaseAttributes = new StartingAttributes { Strength = 1 },
            }
        );
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Strength] = -1,
            }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => generator.Generate(input));
    }

    [Fact]
    public void Generate_AllowsNetReallocation_WhenLoweringOneAttributeFundsAnother()
    {
        // Arrange — base Endurance of 3, 5 points available; the positive-only sum of these
        // deltas (6) would exceed 5, but the net sum (5) does not
        var generator = Builders.MakeCreatureGenerator(
            new CreatureGeneratorOptions
            {
                PointsPerLevel = 5,
                BaseAttributes = new StartingAttributes { Endurance = 3 },
            }
        );
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>
            {
                [AllocatableAttributeName.Endurance] = -1,
                [AllocatableAttributeName.Strength] = 6,
            }
        );

        // Act
        var result = generator.Generate(input);

        // Assert — Strength uses the default base of 5 (only Endurance is overridden above)
        Assert.Equal(11, result.Creature.BaseAttributes.Strength);
        Assert.Equal(2, result.Creature.BaseAttributes.Endurance);
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

        // Assert — default BaseAttributes total 35, PointsPerLevel 5, level 10
        var attributes = result.Creature.BaseAttributes;
        var total =
            attributes.Strength
            + attributes.Defense
            + attributes.Dexterity
            + attributes.Endurance
            + attributes.Stamina
            + attributes.Mana
            + attributes.Intelligence;
        Assert.Equal(35 + 10 * 5, total);
    }

    [Fact]
    public void Generate_ScalesOnlyProfessionSkills_AboveFloor()
    {
        // Act — Knight's profession skills are Melee and Blocking
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 60));

        // Assert — a high enough level makes the random split overwhelmingly likely to raise both above the floor
        var skillLevels = result.Skills.ToDictionary(s => s.Skill, s => s.Level);
        Assert.True(skillLevels[Skill.Melee] > 1);
        Assert.True(skillLevels[Skill.Blocking] > 1);
    }

    [Fact]
    public void Generate_LeavesSkillAtZero_WhenProfessionHasNoAffinityForIt()
    {
        // Act — Knight has no Archery affinity, so it never gets seeded to the level-1 floor
        var result = _creatureGenerator.Generate(MakeInput(Profession.Knight, level: 10));

        // Assert
        var skillLevels = result.Skills.ToDictionary(s => s.Skill, s => s.Level);
        Assert.Equal(0, skillLevels[Skill.Archery]);
    }

    [Fact]
    public void Generate_LeavesSkillsOutsideArchetypeAffinity_AtZero()
    {
        // Act — Baker has no Melee affinity, so it should never learn "Slash" (Melee level 1)
        var result = _creatureGenerator.Generate(MakeInput(Profession.Baker, level: 1));

        // Assert
        Assert.Equal(0, result.Skills.Single(s => s.Skill == Skill.Melee).Level);
    }

    [Fact]
    public void Generate_GrantsBaselineOneInEverySkill_WhenGeneratingThePlayer()
    {
        // Act — Knight has no Archery affinity, but the player can dabble in anything
        var result = _creatureGenerator.Generate(
            MakeInput(
                Profession.Knight,
                level: 1,
                startingAttributeAllocation: new Dictionary<AllocatableAttributeName, int>()
            )
        );

        // Assert
        var skillLevels = result.Skills.ToDictionary(s => s.Skill, s => s.Level);
        Assert.Equal(1, skillLevels[Skill.Archery]);
    }

    [Fact]
    public void Generate_UsesArchetypeCreatureTypeAndNoProfession_ForMonsterArchetypes()
    {
        // Act — the input's Human race must be overridden by the monster archetype
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Beast, 5));

        // Assert
        Assert.Equal(CreatureType.Beast, result.Creature.CreatureType);
        Assert.Null(result.Creature.Profession);
    }

    [Fact]
    public void Generate_GivesNoStartingInventory_ToUnarmedMonsterArchetypes()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Beast, 5));

        // Assert — starting gold still mints its own Gold item, but no gear
        Assert.DoesNotContain(result.Items, i => i is not Gold);
    }

    [Fact]
    public void Generate_ArmsUndead_WithSwordAndArmorButNoAccessories()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Undead, 5));

        // Assert — one sword plus the four armor pieces, and nothing else besides gold
        Assert.Equal(5, result.Items.Count(i => i is not Gold));
    }

    [Fact]
    public void Generate_SetsFixedBiography_ForMonsterArchetypes()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Wraith, 5));

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.Creature.Biography));
    }

    [Fact]
    public void Generate_GivesMonstersSkills_ThatDeriveTheirLevel()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Dragon, 10));

        // Assert
        Assert.Equal(Enum.GetValues<Skill>().Length, result.Skills.Count);
        var skillLevels = result.Skills.Select(s => s.Level).ToArray();
        Assert.Equal(10, SkillFormulas.CalculateLevelFromSkillLevels(skillLevels));
    }

    [Fact]
    public void Generate_CreatesSkills_ForMonsterArchetypes()
    {
        // Act
        var result = _creatureGenerator.Generate(MakeMonsterInput(CreatureArchetype.Giant, 8));

        // Assert
        Assert.NotEmpty(result.Skills);
    }

    [Fact]
    public void Generate_EnsuresCarryingCapacityCoversStartingInventoryWeight()
    {
        // Arrange — a near-zero capacity formula guarantees any starting gear would otherwise
        // exceed capacity, regardless of how Endurance happens to roll
        var generator = Builders.MakeCreatureGenerator(
            new CreatureGeneratorOptions { BaseCarryingCapacity = 0, CarryWeightPerEndurance = 0 }
        );

        // Act — Ranger starts with a bow and 20 arrows, giving it real starting weight
        var result = generator.Generate(MakeInput(Profession.Ranger, level: 1));

        // Assert
        var startingWeight = result.Items.Sum(item => item.Weight * item.Quantity);
        Assert.True(
            startingWeight > 1,
            "Expected the Ranger to start with meaningful gear weight."
        );
        Assert.Equal(startingWeight, result.Creature.CarryingCapacity);
    }
}
