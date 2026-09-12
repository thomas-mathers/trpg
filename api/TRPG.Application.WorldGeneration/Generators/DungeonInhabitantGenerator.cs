using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonInhabitantInput(DungeonGeneratorResult Dungeon, Random Random);

internal record DungeonInhabitantResult(
    CreatureGeneratorResult Participant,
    CreatureProfile Profile,
    IReadOnlyList<CreatureJob> Jobs,
    Guid LocationId
);

public class DungeonInhabitantGenerator(CreatureGenerator creatureGenerator)
{
    private const double SpawnChance = 0.4;

    internal DungeonInhabitantResult? Generate(DungeonInhabitantInput input)
    {
        var spec = DungeonInhabitantCatalog.SpecFor(input.Dungeon.Building.BuildingType);
        if (spec == null || input.Random.NextDouble() >= SpawnChance)
        {
            return null;
        }

        var candidates = input
            .Dungeon.Placements.Where(placement => placement.Role == spec.Role)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var chosen = candidates[input.Random.Next(candidates.Length)];
        var worldId = input.Dungeon.Building.WorldId;

        var generated = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: spec.CreatureType,
                Archetype: spec.Archetype,
                WorldId: worldId,
                BirthLocationId: chosen.Room.LocationId,
                MinLevel: 1,
                MaxLevel: 2
            )
        );
        generated.Creature.LocationId = chosen.Room.LocationId;
        generated.Creature.Biography = spec.Biography;
        // A non-null Profession is how the rest of the codebase tells a named inhabitant apart
        // from a hostile monster spawn — the Goblin archetype otherwise leaves it null.
        generated.Creature.Profession = spec.Profession;

        var profile = new CreatureProfile
        {
            WorldId = worldId,
            CreatureId = generated.Creature.Id,
            Description = spec.Description,
            Behavior = spec.Behavior,
            PrivateBackground = new CreaturePrivateBackground
            {
                Origin = spec.Biography,
                Profession = spec.NarrativeProfession,
            },
        };

        var jobs = new List<CreatureJob>
        {
            CreatureJobGenerator.GenerateSleep(
                generated.Creature.Id,
                chosen.Room.LocationId,
                worldId
            ),
            CreatureJobGenerator.GenerateIdle(
                generated.Creature.Id,
                chosen.Room.LocationId,
                worldId
            ),
        };

        return new DungeonInhabitantResult(generated, profile, jobs, chosen.Room.LocationId);
    }
}
