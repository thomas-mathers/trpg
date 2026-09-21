using TRPG.Application.WorldGeneration.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.WorldGeneration.Generators;

public sealed class FactionRosterGeneratorTests
{
    [Fact]
    public void Generate_TagsBrokenTollWithHumanCreatureType_SoRuntimeRespawnsCanFindIt()
    {
        // Act
        var roster = FactionRosterGenerator.Generate(Guid.NewGuid());

        // Assert — a null CreatureType here means a later wilderness respawn rolling a human
        // raider throws KeyNotFoundException instead of finding this faction.
        Assert.Equal(CreatureType.Human, roster.BrokenToll.CreatureType);
    }
}
