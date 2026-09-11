using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;
using ActiveBuff = TRPG.Application.CreatureFormulas.ActiveBuff;

namespace TRPG.Tests.Application.Creatures;

public class StatFormulasTests
{
    private static Attributes MakeAttributes(
        int strength = 10,
        int endurance = 10,
        int stamina = 10,
        int mana = 10
    )
    {
        return new Attributes
        {
            Strength = strength,
            Endurance = endurance,
            Stamina = stamina,
            Mana = mana,
            FireResistance = 0.25f,
        };
    }

    [Fact]
    public void EffectiveAttribute_ReturnsBaseValue_WithoutModifiers()
    {
        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(strength: 12),
            [],
            [],
            AttributeName.Strength
        );

        // Assert
        Assert.Equal(12f, value);
    }

    [Fact]
    public void EffectiveAttribute_AppliesFlatThenPercent_WhenBothArePresent()
    {
        // Arrange — (10 + 5) × (1 + 20/100) = 18
        ActiveBuff[] buffs =
        [
            new()
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = 5,
                RemainingTurns = 3,
            },
            new()
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Percent,
                Amount = 20,
                RemainingTurns = 3,
            },
        ];

        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(strength: 10),
            buffs,
            [],
            AttributeName.Strength
        );

        // Assert
        Assert.Equal(18f, value);
    }

    [Fact]
    public void EffectiveAttribute_IgnoresModifiers_ForOtherAttributes()
    {
        // Arrange
        ActiveBuff[] buffs =
        [
            new()
            {
                Attribute = AttributeName.Dexterity,
                AmountType = AmountType.Flat,
                Amount = 99,
                RemainingTurns = 3,
            },
        ];

        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(strength: 10),
            buffs,
            [],
            AttributeName.Strength
        );

        // Assert
        Assert.Equal(10f, value);
    }

    [Fact]
    public void EffectiveAttribute_AppliesAttributeModifiers_FromEquippedItems()
    {
        // Arrange
        Item[] inventory =
        [
            new Weapon
            {
                Name = "Test Weapon",
                Modifiers =
                [
                    new AttributeModifier
                    {
                        Attribute = AttributeName.Strength,
                        AmountType = AmountType.Flat,
                        Amount = 5,
                    },
                ],
            },
        ];

        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(strength: 10),
            [],
            inventory,
            AttributeName.Strength
        );

        // Assert
        Assert.Equal(15f, value);
    }

    [Fact]
    public void EffectiveAttribute_AddsArmorAndShieldDefense_ToBaseDefense()
    {
        // Arrange — base 0 + armor 10 + shield 8
        Item[] inventory = [Builders.MakeArmor(), Builders.MakeShield()];

        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(),
            [],
            inventory,
            AttributeName.Defense
        );

        // Assert
        Assert.Equal(18f, value);
    }

    [Fact]
    public void EffectiveAttribute_AddsShieldResistanceModifier_ToBaseResistance()
    {
        // Arrange
        Item[] inventory =
        [
            Builders.MakeShield(
                modifiers:
                [
                    new AttributeModifier
                    {
                        Attribute = AttributeName.FireResistance,
                        AmountType = AmountType.Flat,
                        Amount = 0.15f,
                    },
                ]
            ),
        ];

        // Act
        var value = StatFormulas.CalculateEffectiveAttribute(
            MakeAttributes(),
            [],
            inventory,
            AttributeName.FireResistance
        );

        // Assert
        Assert.Equal(0.4f, value);
    }

    [Fact]
    public void EffectiveAttributes_ResolvesEveryField_ByDelegatingToEffectiveAttribute()
    {
        // Arrange
        ActiveBuff[] buffs =
        [
            new()
            {
                Attribute = AttributeName.Strength,
                AmountType = AmountType.Flat,
                Amount = 5,
                RemainingTurns = 3,
            },
        ];
        Item[] inventory = [Builders.MakeArmor()];

        // Act
        var result = StatFormulas.CalculateEffectiveAttributes(
            MakeAttributes(strength: 10),
            buffs,
            inventory
        );

        // Assert — buffed Strength and gear Defense both landed on the same resolved record
        Assert.Equal(15, result.Strength);
        Assert.Equal(10, result.Defense);
    }

    [Fact]
    public void MaximumMeters_DeriveFromAttributes_WithSensibleFloors()
    {
        // Arrange
        var options = new CreatureGeneratorOptions();

        // Act & Assert — zero endurance still yields a killable 1 HP; zero mana yields no pool
        Assert.Equal(50, StatFormulas.CalculateMaximumHp(MakeAttributes(endurance: 10), options));
        Assert.Equal(1, StatFormulas.CalculateMaximumHp(MakeAttributes(endurance: 0), options));
        Assert.Equal(20, StatFormulas.CalculateMaximumAp(MakeAttributes(stamina: 10), options));
        Assert.Equal(20, StatFormulas.CalculateMaximumMp(MakeAttributes(mana: 10), options));
        Assert.Equal(0, StatFormulas.CalculateMaximumMp(MakeAttributes(mana: 0), options));
    }

    [Fact]
    public void CalculateCarryingCapacity_AddsEnduranceWeightToBase_WithFloorOfOne()
    {
        // Arrange
        var options = new CreatureGeneratorOptions
        {
            BaseCarryingCapacity = 20,
            CarryWeightPerEndurance = 10,
        };

        // Act & Assert
        Assert.Equal(
            120,
            StatFormulas.CalculateCarryingCapacity(MakeAttributes(endurance: 10), options)
        );
        Assert.Equal(
            20,
            StatFormulas.CalculateCarryingCapacity(MakeAttributes(endurance: 0), options)
        );
        Assert.Equal(
            1,
            StatFormulas.CalculateCarryingCapacity(
                MakeAttributes(endurance: -5),
                new CreatureGeneratorOptions
                {
                    BaseCarryingCapacity = 0,
                    CarryWeightPerEndurance = 10,
                }
            )
        );
    }

    private static Attributes MakeStartingAttributes(int strength = 5) =>
        new()
        {
            Strength = strength,
            Defense = 5,
            Dexterity = 5,
            Endurance = 5,
            Stamina = 5,
            Mana = 5,
            Intelligence = 5,
        };

    [Fact]
    public void CalculateUnallocatedAttributePoints_ReturnsExpectedMinusCurrentTotal()
    {
        // Arrange — default options: 7 base stats at 5 each = 35; expected = 35 + level(1) * pointsPerLevel(5) = 40
        // Act
        var unallocated = StatFormulas.CalculateUnallocatedAttributePoints(
            MakeStartingAttributes(),
            level: 1,
            new CreatureGeneratorOptions()
        );

        // Assert
        Assert.Equal(5, unallocated);
    }

    [Fact]
    public void CalculateUnallocatedAttributePoints_ReturnsZero_WhenFullyAllocated()
    {
        // Arrange — spend the 5 available points
        // Act
        var unallocated = StatFormulas.CalculateUnallocatedAttributePoints(
            MakeStartingAttributes(strength: 10),
            level: 1,
            new CreatureGeneratorOptions()
        );

        // Assert
        Assert.Equal(0, unallocated);
    }

    [Fact]
    public void CalculateUnallocatedAttributePoints_GrowsWithCharacterLevel()
    {
        // Arrange — leveling up from 1 to 3 should grant 2 * pointsPerLevel(5) = 10 more points
        // Act
        var unallocated = StatFormulas.CalculateUnallocatedAttributePoints(
            MakeStartingAttributes(),
            level: 3,
            new CreatureGeneratorOptions()
        );

        // Assert
        Assert.Equal(15, unallocated);
    }

    private static Creature MakeCreatureWithMaximums(
        int maximumHp,
        int maximumAp,
        int maximumMp,
        CreatureState state = default
    ) =>
        Builders.MakeCreature(
            currentHp: 0,
            currentAp: 0,
            currentMp: 0,
            state: state,
            baseAttributes: new Attributes
            {
                MaximumHp = maximumHp,
                MaximumAp = maximumAp,
                MaximumMp = maximumMp,
            }
        );

    [Fact]
    public void ApplyPassiveRegen_RegeneratesHpApMp_ProportionalToElapsedInGameHours()
    {
        // Arrange
        var creature = MakeCreatureWithMaximums(maximumHp: 35, maximumAp: 12, maximumMp: 8);
        var options = new CreatureRegenOptions
        {
            HpRegenPercentPerHour = 0.2f,
            ApRegenPercentPerHour = 0.25f,
            MpRegenPercentPerHour = 0.25f,
        };

        // Act
        StatFormulas.ApplyPassiveRegen(creature, GameClock.RealTimePerInGameHour, options);

        // Assert
        Assert.Equal(7, creature.CurrentHp);
        Assert.Equal(3, creature.CurrentAp);
        Assert.Equal(2, creature.CurrentMp);
        Assert.Equal(GameClock.RealTimePerInGameHour, creature.LastRegenPlaytime);
    }

    [Fact]
    public void ApplyPassiveRegen_ClampsAtMaximum_WhenElapsedTimeExceedsFullRegen()
    {
        // Arrange
        var creature = MakeCreatureWithMaximums(maximumHp: 35, maximumAp: 12, maximumMp: 8);
        var options = new CreatureRegenOptions
        {
            HpRegenPercentPerHour = 0.2f,
            ApRegenPercentPerHour = 0.25f,
            MpRegenPercentPerHour = 0.25f,
        };

        // Act
        StatFormulas.ApplyPassiveRegen(creature, TimeSpan.FromHours(100 / 12.0), options);

        // Assert
        Assert.Equal(35, creature.CurrentHp);
        Assert.Equal(12, creature.CurrentAp);
        Assert.Equal(8, creature.CurrentMp);
    }

    [Fact]
    public void ApplyPassiveRegen_DoesNothing_WhenCreatureIsDead()
    {
        // Arrange
        var creature = MakeCreatureWithMaximums(
            maximumHp: 35,
            maximumAp: 12,
            maximumMp: 8,
            state: CreatureState.Dead
        );

        // Act
        StatFormulas.ApplyPassiveRegen(
            creature,
            TimeSpan.FromHours(100 / 12.0),
            new CreatureRegenOptions()
        );

        // Assert
        Assert.Equal(0, creature.CurrentHp);
        Assert.Equal(TimeSpan.Zero, creature.LastRegenPlaytime);
    }

    [Fact]
    public void ApplyPassiveRegen_DoesNothing_WhenElapsedTimeIsZeroOrNegative()
    {
        // Arrange
        var creature = MakeCreatureWithMaximums(maximumHp: 35, maximumAp: 12, maximumMp: 8);
        creature.LastRegenPlaytime = TimeSpan.FromHours(1);

        // Act
        StatFormulas.ApplyPassiveRegen(creature, TimeSpan.FromHours(1), new CreatureRegenOptions());

        // Assert
        Assert.Equal(0, creature.CurrentHp);
        Assert.Equal(TimeSpan.FromHours(1), creature.LastRegenPlaytime);
    }
}
