using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Mappers;

public sealed class CreatureEffectsMapperTests
{
    private static readonly GameInstant ExpiresAt = new(
        new DateTime(2000, 1, 1, 0, 1, 0, DateTimeKind.Unspecified)
    );

    [Fact]
    public void ToEffects_ParsesPersistedEffectNamesIntoEnums()
    {
        // Arrange
        var creature = Builders.MakeCreature(
            activeConditions: new Dictionary<string, GameInstant>
            {
                [nameof(ConditionType.Stunned)] = ExpiresAt,
            },
            activeDots:
            [
                new ActiveDot
                {
                    AbilityName = "Ignite",
                    Amount = 4,
                    DamageType = nameof(DamageType.Fire),
                    ExpiresAt = ExpiresAt,
                },
            ],
            activeHots:
            [
                new ActiveHot
                {
                    AbilityName = "Mend",
                    Amount = 2,
                    ExpiresAt = ExpiresAt,
                },
            ],
            activeBuffs:
            [
                new ActiveBuff
                {
                    AbilityName = "Bulwark",
                    Amount = 10,
                    Attribute = nameof(AttributeName.Defense),
                    AmountType = nameof(AmountType.Flat),
                    ExpiresAt = ExpiresAt,
                },
            ]
        );

        // Act
        var effects = creature.ToEffects();

        // Assert
        Assert.Equal(ExpiresAt, effects.Conditions[ConditionType.Stunned]);
        Assert.Equal(
            new CreatureDotEffect("Ignite", 4, DamageType.Fire, ExpiresAt),
            Assert.Single(effects.Dots)
        );
        Assert.Equal(new CreatureHotEffect("Mend", 2, ExpiresAt), Assert.Single(effects.Hots));
        Assert.Equal(
            new CreatureBuffEffect(
                "Bulwark",
                AttributeName.Defense,
                10,
                AmountType.Flat,
                ExpiresAt
            ),
            Assert.Single(effects.Buffs)
        );
    }
}
