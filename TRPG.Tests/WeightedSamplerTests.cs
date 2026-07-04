using TRPG.Generators;

namespace TRPG.Tests;

public class WeightedSamplerTests {
    [Fact]
    public void SampleIndex_FavorsHeavilyWeightedCandidate() {
        // Arrange
        var weights = new[] { 1, 1, 100 };
        var counts = new int[3];

        // Act
        for (var i = 0; i < 1000; i++) {
            counts[WeightedSampler.SampleIndex(weights)]++;
        }

        // Assert
        Assert.True(counts[2] > 900, $"Expected index 2 to dominate, got counts: {string.Join(",", counts)}");
    }

    [Fact]
    public void SampleIndex_ReturnsValidIndex_WhenAllWeightsAreZero() {
        // Arrange
        var weights = new[] { 0, 0, 0 };

        // Act
        var index = WeightedSampler.SampleIndex(weights);

        // Assert
        Assert.InRange(index, 0, weights.Length - 1);
    }

    [Fact]
    public void SampleIndex_ReturnsZero_ForSingleElement() {
        // Arrange
        var weights = new[] { 5 };

        // Act
        var index = WeightedSampler.SampleIndex(weights);

        // Assert
        Assert.Equal(0, index);
    }
}