using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Common.Tools;
using TRPG.Data.Models;

namespace TRPG.Tests.Application.Combat;

public class CombatEventTests
{
    [Fact]
    public void Hit_OmitsAttackerAndTargetIds_WhenSerializedForLlmNarration()
    {
        // Arrange
        var hit = new Hit(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith",
            TargetRemainingHp: 5,
            TargetMaximumHp: 20,
            Killed: false,
            IsCritical: true,
            Damage: 15,
            DamageType: DamageType.Physical,
            AppliedConditions: []
        );

        // Act
        var json = JsonSerializer.Serialize(hit, ToolJsonOptions.Options);

        // Assert
        Assert.DoesNotContain("AttackerId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetId", json, StringComparison.Ordinal);
        Assert.Contains("Hero", json, StringComparison.Ordinal);
        Assert.Contains("Wraith", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Miss_OmitsAttackerAndTargetIds_WhenSerializedForLlmNarration()
    {
        // Arrange
        var miss = new Miss(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith"
        );

        // Act
        var json = JsonSerializer.Serialize(miss, ToolJsonOptions.Options);

        // Assert
        Assert.DoesNotContain("AttackerId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetId", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Block_OmitsAttackerAndTargetIds_WhenSerializedForLlmNarration()
    {
        // Arrange
        var block = new Block(
            AttackerId: Guid.NewGuid(),
            AttackerName: "Hero",
            AbilityName: "Slash",
            TargetId: Guid.NewGuid(),
            TargetName: "Wraith"
        );

        // Act
        var json = JsonSerializer.Serialize(block, ToolJsonOptions.Options);

        // Assert
        Assert.DoesNotContain("AttackerId", json, StringComparison.Ordinal);
        Assert.DoesNotContain("TargetId", json, StringComparison.Ordinal);
    }
}
