using TRPG.Application.Abilities;
using TRPG.Application.Common;
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
            new TestOptionsSnapshot<CreatureGeneratorOptions>(new CreatureGeneratorOptions())
        );
    }

    private CreatureGeneratorInput MakeInput(Profession profession, int level = 0)
    {
        return new CreatureGeneratorInput(
            CreatureType.Human,
            profession,
            _worldId,
            _stateId,
            _stateId,
            Level: level
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
        Assert.Equal(result.Creature.Attributes.MaximumHp, result.Creature.CurrentHp);
        Assert.Equal(result.Creature.Attributes.MaximumAp, result.Creature.CurrentAp);
        Assert.Equal(result.Creature.Attributes.MaximumMp, result.Creature.CurrentMp);
        Assert.Equal(TimeSpan.Zero, result.Creature.LastRegenPlaytime);
    }
}
