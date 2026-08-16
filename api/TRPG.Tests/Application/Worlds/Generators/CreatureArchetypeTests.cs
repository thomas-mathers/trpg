using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class CreatureArchetypeTests
{
    [Fact]
    public void For_ReturnsAMatchingArchetype_ForEveryProfession()
    {
        // Assert — a profession added to the enum without a registry row would otherwise
        // only surface as a KeyNotFoundException in the middle of world generation
        Assert.All(
            Enum.GetValues<Profession>(),
            profession => Assert.Equal(profession, CreatureArchetype.For(profession).Profession)
        );
    }
}
