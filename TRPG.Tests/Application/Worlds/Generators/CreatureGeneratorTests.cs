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

    private static CreatureGenerator MakeCreatureGenerator()
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
            new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions()),
            Builders.MakeStatFormulas()
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
            startingAttributeAllocation: new Dictionary<AttributeName, int> { [AttributeName.Strength] = 5 }
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
            startingAttributeAllocation: new Dictionary<AttributeName, int> { [AttributeName.Mana] = 2 }
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
            startingAttributeAllocation: new Dictionary<AttributeName, int> { [AttributeName.Strength] = 6 }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_Throws_WhenAllocationHasNegativeDelta()
    {
        // Arrange
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int> { [AttributeName.Strength] = -1 }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }

    [Fact]
    public void Generate_Throws_WhenAllocationTargetsDefense()
    {
        // Arrange — Defense is not player-allocatable
        var input = MakeInput(
            Profession.Knight,
            level: 1,
            startingAttributeAllocation: new Dictionary<AttributeName, int> { [AttributeName.Defense] = 1 }
        );

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _creatureGenerator.Generate(input));
    }
}
