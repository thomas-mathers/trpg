using TRPG.Application.Creatures.Results;
using TRPG.Application.WorldSimulation.Publishing;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes;

public sealed class SceneVitalsPatchTests
{
    [Fact]
    public void Apply_PatchesTheMatchingNearbyCreature_AndLeavesTheOthersAlone()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene(others: ["Ada", "Bram"]);
        var patched = scene.NearbyCreatures.First();
        var untouched = scene.NearbyCreatures.Last();
        var vitals = new CreatureVitals(patched.Id, 7, 10, 1, 2, 3, 4);

        // Act
        var result = SceneVitalsPatch.Apply(scene, [vitals]);

        // Assert
        Assert.Equal(
            7,
            result.NearbyCreatures.Single(creature => creature.Id == patched.Id).CurrentHp
        );
        Assert.Equal(
            untouched,
            result.NearbyCreatures.Single(creature => creature.Id == untouched.Id)
        );
    }

    [Fact]
    public void Apply_PatchesThePlayer_WhenTheVitalsBelongToThePlayer()
    {
        // Arrange
        var scene = SceneResultBuilder.MakeScene();
        var vitals = new CreatureVitals(scene.Player.Id, 3, 10, 1, 2, 3, 4);

        // Act
        var result = SceneVitalsPatch.Apply(scene, [vitals]);

        // Assert
        Assert.Equal(3, result.Player.CurrentHp);
    }
}
