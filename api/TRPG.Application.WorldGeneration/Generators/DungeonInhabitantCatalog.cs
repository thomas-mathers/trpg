using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record DungeonInhabitantSpec(
    RoomRole Role,
    CreatureType CreatureType,
    CreatureArchetype Archetype,
    Profession Profession,
    string NarrativeProfession,
    string Description,
    CreatureBehavior Behavior,
    string Biography
);

// A dungeon that only ever holds monsters and the dead reads as a diorama. One inhabitant per
// dungeon type gives its signature room someone still there on their own business, worth talking to.
internal static class DungeonInhabitantCatalog
{
    private static readonly Dictionary<BuildingType, DungeonInhabitantSpec> SpecsByDungeonType =
        new()
        {
            [BuildingType.Cave] = new DungeonInhabitantSpec(
                Role: RoomRole.Storeroom,
                CreatureType: CreatureType.Goblin,
                Archetype: CreatureArchetype.For(CreatureType.Goblin),
                Profession: Profession.Unemployed,
                NarrativeProfession: "Scavenger",
                Description: "A small goblin picking through crates, sorting what's worth carrying off from what isn't.",
                Behavior: new CreatureBehavior
                {
                    Personality = "Twitchy and quick to bargain rather than fight.",
                    SpeechStyle = "Clipped, self-interested, prone to haggling.",
                    Hobby = "Hoarding anything that glints.",
                },
                Biography: "A lone scavenger who slipped away from its pack to work this room undisturbed."
            ),
            [BuildingType.Mine] = new DungeonInhabitantSpec(
                Role: RoomRole.CollapsedGallery,
                CreatureType: CreatureType.Human,
                Archetype: CreatureArchetype.For(Profession.Unemployed),
                Profession: Profession.Unemployed,
                NarrativeProfession: "Prospector",
                Description: "A prospector wedged into the rubble, working loose a way through by lamplight.",
                Behavior: new CreatureBehavior
                {
                    Personality = "Stubborn and single-minded about the vein they came here for.",
                    SpeechStyle = "Terse, distracted by the work.",
                    Hobby = "Panning for ore in every puddle.",
                },
                Biography: "Came in after a rumored vein and got cut off when the gallery came down."
            ),
            [BuildingType.Crypt] = new DungeonInhabitantSpec(
                Role: RoomRole.Shrine,
                CreatureType: CreatureType.Human,
                Archetype: CreatureArchetype.For(Profession.Cleric),
                Profession: Profession.Cleric,
                NarrativeProfession: "Caretaker",
                Description: "An old caretaker still tending the shrine, straightening offerings no one else remembers to leave.",
                Behavior: new CreatureBehavior
                {
                    Personality = "Patient and quietly devoted.",
                    SpeechStyle = "Measured, formal, given to old phrases.",
                    Hobby = "Cataloguing the names on every tomb.",
                },
                Biography: "Has kept this shrine long after everyone who used to visit it stopped coming."
            ),
            [BuildingType.Ruins] = new DungeonInhabitantSpec(
                Role: RoomRole.Study,
                CreatureType: CreatureType.Human,
                Archetype: CreatureArchetype.For(Profession.Scholar),
                Profession: Profession.Scholar,
                NarrativeProfession: "Scholar",
                Description: "A scholar hunched over salvaged pages, piecing together who lived here.",
                Behavior: new CreatureBehavior
                {
                    Personality = "Absorbed in the work to the point of rudeness.",
                    SpeechStyle = "Precise, footnoted, easily sidetracked into lecture.",
                    Hobby = "Transcribing anything with writing on it.",
                },
                Biography: "Came to study the ruins' former occupants and hasn't left since."
            ),
            [BuildingType.Tower] = new DungeonInhabitantSpec(
                Role: RoomRole.Study,
                CreatureType: CreatureType.Human,
                Archetype: CreatureArchetype.For(Profession.Mage),
                Profession: Profession.Mage,
                NarrativeProfession: "Apprentice",
                Description: "A young apprentice still keeping their master's study, waiting for a return that hasn't come.",
                Behavior: new CreatureBehavior
                {
                    Personality =
                        "Anxious and dutiful, deferring to an authority that isn't there anymore.",
                    SpeechStyle = "Formal when nervous, which is often.",
                    Hobby = "Practicing spells they were never quite taught properly.",
                },
                Biography: "Stayed at their master's tower after the master disappeared, still keeping the study in order."
            ),
        };

    public static DungeonInhabitantSpec? SpecFor(BuildingType dungeonType) =>
        SpecsByDungeonType.GetValueOrDefault(dungeonType);
}
