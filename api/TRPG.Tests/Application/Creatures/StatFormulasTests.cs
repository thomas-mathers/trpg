using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Data.Models;
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
        Item[] inventory = [Builders.MakeArmorItem(), Builders.MakeShieldItem()];

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
            Builders.MakeShieldItem(
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
        Item[] inventory = [Builders.MakeArmorItem()];

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
}
