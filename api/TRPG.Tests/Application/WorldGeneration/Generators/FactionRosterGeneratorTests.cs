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

    [Fact]
    public void Generate_GivesBrokenTollAHostileDisposition_SoShakedownsCanInitiate()
    {
        var roster = FactionRosterGenerator.Generate(Guid.NewGuid());

        Assert.Equal(100, roster.BrokenToll.Aggression);
        Assert.Equal(EncounterApproach.Shakedown, roster.BrokenToll.EncounterApproach);
        Assert.Equal(50, roster.BrokenToll.ReputationSensitivity);
        Assert.Equal(35, roster.BrokenToll.RiskAversion);
        Assert.Equal(FactionTemperament.Predatory, roster.BrokenToll.Temperament);
    }

    [Fact]
    public void Generate_DoesNotInferEncounterBehaviorFromFactionKind()
    {
        var roster = FactionRosterGenerator.Generate(Guid.NewGuid());

        Assert.All(
            roster.AntagonistFactions,
            faction => Assert.Equal(EncounterApproach.None, faction.EncounterApproach)
        );
    }
}
