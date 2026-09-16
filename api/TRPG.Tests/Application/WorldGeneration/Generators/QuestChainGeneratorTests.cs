using Microsoft.Extensions.Logging.Abstractions;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public class QuestChainGeneratorTests
{
    [Fact]
    public async Task Generate_MapsFakeSchemaIntoTypedResult()
    {
        // Arrange
        var generator = new QuestChainGenerator(
            new FakeChatClient(),
            NullLogger<QuestChainGenerator>.Instance
        );
        var entity = new QuestChainCandidateEntity(
            Guid.NewGuid(),
            "Old Man Hendricks",
            QuestChainEntityTypes.Creature
        );
        var input = new QuestChainGeneratorInput
        {
            ChainPremise = "A test premise.",
            ChainLength = 1,
            AvailableEntities = [entity],
        };

        // Act
        var result = await generator.Generate(input, TestContext.Current.CancellationToken);

        // Assert
        var node = Assert.Single(result);
        Assert.Equal(entity.Id, node.GiverEntityId);
        var objective = Assert.Single(node.Objectives);
        Assert.Equal(GeneratedObjectiveType.SpeakToCreature, objective.ObjectiveType);
        Assert.Equal(entity.Id, objective.TargetEntityId);
    }
}
