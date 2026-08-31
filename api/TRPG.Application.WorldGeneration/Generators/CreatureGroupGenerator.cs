using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record CreatureGroupGeneratorInput(
    CreatureType DominantRace,
    Profession Profession,
    Guid WorldId,
    Guid LocationId,
    int Count,
    int MinLevel,
    int MaxLevel
);

public class CreatureGroupGenerator(CreatureGenerator creatureGenerator)
{
    private const int AdultAge = 18;
    private const int MaxAdultBirthYear = WorldEpoch.Year - AdultAge;

    internal IReadOnlyList<CreatureGeneratorResult> Generate(CreatureGroupGeneratorInput input)
    {
        var members = new List<CreatureGeneratorResult>();
        for (var i = 0; i < input.Count; i++)
        {
            var memberRace = CreatureGenerator.PickCreatureType(input.DominantRace);
            var member = creatureGenerator.Generate(
                new CreatureGeneratorInput(
                    memberRace,
                    CreatureArchetype.For(input.Profession),
                    input.WorldId,
                    input.LocationId,
                    MinLevel: input.MinLevel,
                    MaxLevel: input.MaxLevel
                )
                {
                    MaxBirthYear = MaxAdultBirthYear,
                }
            );
            member.Creature.LocationId = input.LocationId;
            members.Add(member);
        }

        return members;
    }
}
