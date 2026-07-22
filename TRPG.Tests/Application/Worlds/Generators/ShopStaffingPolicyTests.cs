using TRPG.Application.Worlds.Generators;

namespace TRPG.Tests.Application.Worlds.Generators;

public class ShopStaffingPolicyTests
{
    [Fact]
    public void StaffDayOffPatterns_AreAllPairwiseDisjoint()
    {
        // Arrange
        var patterns = ShopStaffingPolicy.StaffDayOffPatterns;

        // Act & Assert — this is what guarantees a shop is never fully unstaffed regardless of who's hired
        for (var i = 0; i < patterns.Length; i++)
        {
            for (var j = i + 1; j < patterns.Length; j++)
            {
                Assert.Empty(patterns[i].Intersect(patterns[j]));
            }
        }
    }

    [Fact]
    public void NonOverlappingDayOffPatterns_PartitionTheWeekWithNoOverlap()
    {
        // Act
        var patterns = ShopStaffingPolicy.NonOverlappingDayOffPatterns;

        // Assert — the two positions never share a day off (so someone's always covering the single workstation),
        // and together their days off cover the whole week (each works exactly the days the other is off)
        Assert.Empty(patterns[0].Intersect(patterns[1]));
        Assert.Equal(7, patterns[0].Concat(patterns[1]).Distinct().Count());
    }
}
