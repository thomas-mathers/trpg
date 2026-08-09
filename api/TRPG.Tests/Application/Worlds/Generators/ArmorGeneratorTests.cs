using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public sealed class ArmorGeneratorTests
{
    [Fact]
    public void GenerateShield_AddsFlatResistanceModifiers()
    {
        // Act
        var shield = new ArmorGenerator().GenerateShield(level: 1, Guid.NewGuid());

        // Assert
        foreach (
            var attribute in new[]
            {
                AttributeName.MagicResistance,
                AttributeName.FireResistance,
                AttributeName.IceResistance,
                AttributeName.LightningResistance,
                AttributeName.PoisonResistance,
            }
        )
        {
            var modifier = Assert.Single(
                shield.Modifiers.OfType<AttributeModifier>(),
                modifier => modifier.Attribute == attribute
            );
            Assert.Equal(AmountType.Flat, modifier.AmountType);
            Assert.True(modifier.Amount > 0);
        }
    }
}
