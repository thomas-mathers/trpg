using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonExpeditionInput(
    IReadOnlyList<DungeonGeneratorResult> Dungeons,
    IReadOnlyCollection<CreatureSpawner> Spawners,
    IReadOnlyCollection<Prop> Props,
    IReadOnlyCollection<Guid> InhabitantLocationIds,
    Random Random
);

internal record DungeonExpeditionResult(
    DungeonExpedition Expedition,
    IReadOnlyList<CreatureGeneratorResult> Participants,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<CreatureProfile> Profiles,
    Book Journal,
    BookWork Work,
    Secret Secret
);

public class DungeonExpeditionGenerator(CreatureGenerator creatureGenerator)
{
    internal DungeonExpeditionResult? Generate(DungeonExpeditionInput input)
    {
        var occupied = input.Spawners.Select(spawner => spawner.LocationId).ToHashSet();
        occupied.UnionWith(input.Props.OfType<Trigger>().Select(trigger => trigger.LocationId));
        occupied.UnionWith(input.InhabitantLocationIds);
        foreach (var dungeon in input.Dungeons.OrderBy(_ => input.Random.Next()))
        {
            var routeTree = BuildRouteTree(dungeon);
            var candidates = dungeon
                .Placements.Where(placement =>
                    placement.DepthFromEntrance >= 2
                    && placement.Role != RoomRole.BossChamber
                    && !occupied.Contains(placement.Room.LocationId)
                )
                .Select(placement =>
                    (
                        Placement: placement,
                        Route: RouteTo(dungeon, routeTree, placement.Room.LocationId)
                    )
                )
                .Where(candidate => candidate.Route.Count >= 3)
                .ToArray();
            if (candidates.Length > 0)
            {
                var chosen = candidates[input.Random.Next(candidates.Length)];
                return Create(dungeon, chosen.Placement, chosen.Route);
            }
        }

        return null;
    }

    private DungeonExpeditionResult Create(
        DungeonGeneratorResult dungeon,
        DungeonRoomPlacement placement,
        IReadOnlyList<Guid> route
    )
    {
        var survivor = CreateExplorer(dungeon.Building.WorldId, dungeon.EntranceLocationId);
        var companion = CreateExplorer(dungeon.Building.WorldId, placement.Room.LocationId);
        companion.Creature.State = CreatureState.Dead;
        companion.Creature.CurrentHp = 0;
        var expedition = Describe(dungeon, placement, route, survivor.Creature, companion.Creature);
        return BuildResult(expedition, survivor, companion);
    }

    private static DungeonExpedition Describe(
        DungeonGeneratorResult dungeon,
        DungeonRoomPlacement placement,
        IReadOnlyList<Guid> route,
        Creature survivor,
        Creature companion
    ) =>
        new()
        {
            WorldId = dungeon.Building.WorldId,
            BuildingId = dungeon.Building.Id,
            SurvivorId = survivor.Id,
            CompanionId = companion.Id,
            SurvivorName = survivor.Name,
            CompanionName = companion.Name,
            EntranceLocationId = dungeon.EntranceLocationId,
            CompanionLocationId = placement.Room.LocationId,
            JournalWorkId = Guid.NewGuid(),
            JournalItemId = Guid.NewGuid(),
            DiscoverySecretId = Guid.NewGuid(),
            Purpose =
                $"{survivor.Name} and {companion.Name} came to document the remains of {dungeon.Building.Name}.",
            Separation =
                $"They explored together as far as {dungeon.Placements.Single(room => room.Room.LocationId == route[^2]).Room.Name}. {survivor.Name} returned to their supplies at the entrance while {companion.Name} continued alone and never returned.",
            FinalExperience =
                $"After a fall, the author reached {placement.Room.Name}, too badly injured to make the return journey, and wrote a final account while waiting for help.",
            Discovery =
                $"{companion.Name}'s final journal entry records taking shelter in {placement.Room.Name} after a fall, unable to return to {survivor.Name} at the entrance.",
            KnownRouteLocationIds = route.SkipLast(1).ToList(),
        };

    // Traversed once per dungeon and reused for every candidate room, rather than a fresh BFS per lookup.
    private static IReadOnlyDictionary<Guid, Guid> BuildRouteTree(DungeonGeneratorResult dungeon)
    {
        var previous = new Dictionary<Guid, Guid>();
        var pending = new Queue<Guid>();
        pending.Enqueue(dungeon.EntranceLocationId);
        previous[dungeon.EntranceLocationId] = dungeon.EntranceLocationId;
        while (pending.TryDequeue(out var current))
        {
            foreach (
                var connector in dungeon.LocationConnectors.Where(connector =>
                    connector.OriginLocationId == current
                )
            )
            {
                var next = connector.DestinationLocationId;
                if (
                    next != dungeon.BossLocationId
                    && dungeon.Locations.Any(location => location.Id == next)
                    && previous.TryAdd(next, current)
                )
                    pending.Enqueue(next);
            }
        }
        return previous;
    }

    private static IReadOnlyList<Guid> RouteTo(
        DungeonGeneratorResult dungeon,
        IReadOnlyDictionary<Guid, Guid> routeTree,
        Guid destination
    )
    {
        if (!routeTree.ContainsKey(destination))
        {
            return [];
        }

        var route = new List<Guid> { destination };
        var current = destination;
        while (current != dungeon.EntranceLocationId)
        {
            current = routeTree[current];
            route.Add(current);
        }
        route.Reverse();
        return route.ToArray();
    }

    private CreatureGeneratorResult CreateExplorer(Guid worldId, Guid locationId)
    {
        var generated = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: CreatureType.Human,
                Archetype: CreatureArchetype.For(Profession.Ranger),
                WorldId: worldId,
                BirthLocationId: locationId,
                MinLevel: 2,
                MaxLevel: 2
            )
        );
        generated.Creature.LocationId = locationId;
        return generated;
    }

    private static CreatureProfile CreateProfile(Creature creature) =>
        new()
        {
            WorldId = creature.WorldId,
            CreatureId = creature.Id,
            Description =
                "A travel-worn explorer with ink-stained fingers and a battered field pack.",
            Behavior = new CreatureBehavior
            {
                Personality = "Watchful and devoted to their travelling companion.",
                SpeechStyle = "Practical, precise descriptions.",
                Hobby = "Sketching old buildings.",
            },
            PrivateBackground = new CreaturePrivateBackground
            {
                Origin = creature.Biography,
                Profession = "Explorer",
            },
        };

    private static DungeonExpeditionResult BuildResult(
        DungeonExpedition expedition,
        CreatureGeneratorResult survivor,
        CreatureGeneratorResult companion
    )
    {
        survivor.Creature.Biography = $"{expedition.Purpose} {expedition.Separation}";
        companion.Creature.Biography = $"An explorer and companion of {expedition.SurvivorName}.";
        var title = $"{expedition.CompanionName}'s expedition journal";
        return new DungeonExpeditionResult(
            expedition,
            [survivor, companion],
            [
                CreatureJobGenerator.GenerateSleep(
                    survivor.Creature.Id,
                    expedition.EntranceLocationId,
                    expedition.WorldId
                ),
                CreatureJobGenerator.GenerateIdle(
                    survivor.Creature.Id,
                    expedition.EntranceLocationId,
                    expedition.WorldId
                ),
            ],
            [CreateProfile(survivor.Creature), CreateProfile(companion.Creature)],
            CreateJournal(expedition, title),
            CreateWork(expedition, title),
            new Secret
            {
                Id = expedition.DiscoverySecretId,
                WorldId = expedition.WorldId,
                Subject = "the expedition's final account",
                Value = expedition.Discovery,
            }
        );
    }

    private static Book CreateJournal(DungeonExpedition expedition, string title) =>
        new()
        {
            Id = expedition.JournalItemId,
            WorldId = expedition.WorldId,
            WorkId = expedition.JournalWorkId,
            Name = title,
            Description = "A battered journal, its final page marked by a trembling hand.",
            Quantity = 1,
            Weight = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = expedition.CompanionId,
                OwnerType = OwnerType.Creature,
            },
        };

    private static BookWork CreateWork(DungeonExpedition expedition, string title) =>
        new()
        {
            Id = expedition.JournalWorkId,
            WorldId = expedition.WorldId,
            Title = title,
            Tier = BookTier.Clue,
            SubjectType = BookSubjectType.Building,
            SubjectName = expedition.Purpose,
            PageCount = 1,
            SecretId = expedition.DiscoverySecretId,
            SecretPageNumber = 1,
        };
}
