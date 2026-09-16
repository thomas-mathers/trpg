using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class CreatureEpithetGeneratorTests
{
    [Fact]
    public void ComposeName_AppendsAnEpithetAfterTheBaseName()
    {
        // Act
        var composed = CreatureEpithetGenerator.ComposeName("Grukk Ashclaw", new Random(1));

        // Assert
        Assert.StartsWith("Grukk Ashclaw ", composed);
        Assert.True(composed.Length > "Grukk Ashclaw ".Length);
    }

    [Fact]
    public void ComposeName_CanProduceDifferentEpithets_AcrossDifferentSeeds()
    {
        // Act
        var composedNames = Enumerable
            .Range(0, 20)
            .Select(seed => CreatureEpithetGenerator.ComposeName("Grukk", new Random(seed)))
            .Distinct()
            .ToArray();

        // Assert
        Assert.True(composedNames.Length > 1);
    }
}
