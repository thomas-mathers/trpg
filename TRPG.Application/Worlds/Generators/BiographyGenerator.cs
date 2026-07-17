using System.Globalization;
using TRPG.Application.Game;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal record BiographyGeneratorInput(
    IReadOnlyList<Creature> Creatures,
    IReadOnlyDictionary<Guid, State> StateById,
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<Faction> Factions,
    IReadOnlyList<Relationship> Relationships,
    IReadOnlyList<CreatureJob> Jobs,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Building> Buildings,
    IReadOnlyList<BuildingOwner> BuildingOwners
);

internal static class BiographyGenerator
{
    private static readonly (string Name, string HighPhrase, string LowPhrase)[] StatDescriptors =
    [
        ("Strength", "strong", "frail"),
        ("Defense", "tough", "easily wounded"),
        ("Dexterity", "nimble", "clumsy"),
        ("Endurance", "tireless", "easily winded"),
        ("Stamina", "energetic", "listless"),
        ("Mana", "attuned to magic", "utterly unmagical"),
        ("Intelligence", "sharp-witted", "plainspoken"),
    ];

    private static readonly (
        string Trait,
        string HighPhrase,
        string LowPhrase
    )[] OceanDescriptors =
    [
        ("Openness", "endlessly curious about new ideas", "set in their ways"),
        ("Conscientiousness", "disciplined", "easily distracted"),
        ("Extraversion", "gregarious", "reserved, saying little unless asked"),
        ("Agreeableness", "quick to trust", "blunt, sometimes to the point of rudeness"),
        ("Neuroticism", "prone to overthinking", "unshakably calm, rarely rattled"),
    ];

    private static readonly string[] Hobbies =
    [
        "woodworking",
        "chess",
        "gardening",
        "storytelling",
        "fishing",
        "collecting old coins",
        "sketching",
        "brewing",
        "birdwatching",
        "poetry",
        "whittling",
        "foraging",
        "stargazing",
        "dice games",
        "herbalism",
        "singing",
        "dancing",
        "tinkering",
        "falconry",
        "cartography",
    ];

    private static readonly string[] PhysicalQuirks =
    [
        "A jagged scar runs along one forearm, a memento from some old mishap.",
        "A streak of gray hair stands out, unusual for their age.",
        "They walk with a faint limp from an old injury.",
        "A chipped front tooth shows whenever they smile.",
        "Their hands are calloused from years of hard labor.",
        "Their nose was broken once and never quite set straight.",
        "A small, faded tattoo of unclear origin marks one wrist.",
        "Ink stains permanently mark their fingertips.",
        "One fingertip is missing, the story rarely told the same way twice.",
        "Their skin is weathered and sun-browned from years outdoors.",
        "A distinctive gap shows between their front teeth.",
        "Their eyes are two slightly different shades.",
        "A faded burn mark covers the back of one hand.",
        "They have a habit of fidgeting with a ring on their finger.",
        "Their skin is unusually pale, almost ghostlike.",
        "They wear their hair in a long braid, unusual for their kind.",
        "Their knuckles are scarred from old fights, half-forgotten.",
        "Their hands have a soft, well-worn look about them.",
    ];

    private static readonly string[] SpeechPatternTemplates =
    [
        "They speak in clipped, direct sentences, a manner common to {0}.",
        "They speak slowly and deliberately, weighing each word, as many from {0} do.",
        "Their voice carries a musical, lilting quality typical of {0}.",
        "They are blunt and unadorned in speech, plainspoken like most from {0}.",
        "Their speech is formal and old-fashioned, a habit picked up in {0}.",
        "They speak quickly and cleverly, often turning a phrase, much like others from {0}.",
        "They tend to speak quietly, almost a murmur, as is common in {0}.",
        "They speak loudly and boisterously, a trait shared by many from {0}.",
        "They are precise and methodical in speech, often pausing to choose exactly the right word.",
        "Their speech is warm and folksy, carrying the easy manner of {0}.",
        "They are terse, saying little more than necessary, much like others raised in {0}.",
        "They tend to wander into long, winding tangents when they speak.",
    ];

    public static void AssignBiographies(BiographyGeneratorInput input)
    {
        var factionById = input.Factions.ToDictionary(f => f.Id);
        var factionNameByCreatureId = input
            .FactionMembers.Where(fm => !factionById[fm.FactionId].IsCityFaction)
            .GroupBy(fm => fm.CreatureId)
            .ToDictionary(g => g.Key, g => factionById[g.First().FactionId].Name);

        var creatureNameById = input.Creatures.ToDictionary(c => c.Id, c => c.Name);
        var parentNamesByCreatureId = input
            .Relationships.Where(r =>
                r.RelationshipType is RelationshipType.Mother or RelationshipType.Father
            )
            .GroupBy(r => r.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(r => creatureNameById[r.RelativeId]).ToArray());
        var childNamesByCreatureId = input
            .Relationships.Where(r =>
                r.RelationshipType is RelationshipType.Son or RelationshipType.Daughter
            )
            .GroupBy(r => r.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(r => creatureNameById[r.RelativeId]).ToArray());
        var siblingNamesByCreatureId = input
            .Relationships.Where(r =>
                r.RelationshipType is RelationshipType.Brother or RelationshipType.Sister
            )
            .GroupBy(r => r.SubjectId)
            .ToDictionary(g => g.Key, g => g.Select(r => creatureNameById[r.RelativeId]).ToArray());
        var spouseByCreatureId = input
            .Relationships.Where(r =>
                r.RelationshipType is RelationshipType.Husband or RelationshipType.Wife
            )
            .ToDictionary(
                r => r.SubjectId,
                r => new SpouseInfo(
                    r.RelationshipType == RelationshipType.Husband ? "husband" : "wife",
                    creatureNameById[r.RelativeId]
                )
            );

        var buildingIdByRoomId = input.Rooms.ToDictionary(r => r.Id, r => r.BuildingId);
        var buildingById = input.Buildings.ToDictionary(b => b.Id);
        var workJobByCreatureId = input
            .Jobs.Where(j => j.Action == CreatureJobAction.Work)
            .GroupBy(j => j.CreatureId)
            .ToDictionary(g => g.Key, g => g.First());
        var sleepJobByCreatureId = input
            .Jobs.Where(j => j.Action == CreatureJobAction.Sleep)
            .GroupBy(j => j.CreatureId)
            .ToDictionary(g => g.Key, g => g.First());
        var ownedBuildingIdsByCreatureId = input
            .BuildingOwners.GroupBy(bo => bo.OwnerId)
            .ToDictionary(g => g.Key, g => g.Select(bo => bo.BuildingId).ToHashSet());
        var daysOffByCreatureId = input
            .Jobs.Where(j => j.SpecificDay != null)
            .GroupBy(j => j.CreatureId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(j => j.SpecificDay!.Value).Distinct().OrderBy(d => (int)d).ToArray()
            );

        foreach (var creature in input.Creatures)
        {
            factionNameByCreatureId.TryGetValue(creature.Id, out var factionName);
            parentNamesByCreatureId.TryGetValue(creature.Id, out var parentNames);
            childNamesByCreatureId.TryGetValue(creature.Id, out var childNames);
            siblingNamesByCreatureId.TryGetValue(creature.Id, out var siblingNames);
            spouseByCreatureId.TryGetValue(creature.Id, out var spouse);
            workJobByCreatureId.TryGetValue(creature.Id, out var workJob);
            sleepJobByCreatureId.TryGetValue(creature.Id, out var sleepJob);
            ownedBuildingIdsByCreatureId.TryGetValue(creature.Id, out var ownedBuildingIds);
            var workplace = ResolveBuilding(workJob, buildingIdByRoomId, buildingById);
            var homeName = ResolveBuilding(sleepJob, buildingIdByRoomId, buildingById)?.Name;
            var ownsWorkplace =
                workplace != null
                && ownedBuildingIds != null
                && ownedBuildingIds.Contains(workplace.Value.BuildingId);
            daysOffByCreatureId.TryGetValue(creature.Id, out var daysOff);
            var daysOffOrNull = workJob != null ? daysOff : null;
            var birthplaceName = input.StateById[creature.BirthStateId].Name;
            creature.Biography = BuildBiography(
                creature,
                birthplaceName,
                factionName,
                parentNames,
                childNames,
                siblingNames,
                spouse,
                workplace?.Name,
                workJob,
                ownsWorkplace,
                daysOffOrNull,
                homeName
            );
        }
    }

    private sealed record SpouseInfo(string Label, string Name);

    private static string BuildBiography(
        Creature creature,
        string birthplaceName,
        string? factionName,
        IReadOnlyList<string>? parentNames,
        IReadOnlyList<string>? childNames,
        IReadOnlyList<string>? siblingNames,
        SpouseInfo? spouse,
        string? workplaceName,
        CreatureJob? workJob,
        bool ownsWorkplace,
        DayOfWeek[]? daysOff,
        string? homeName
    )
    {
        var age = GameClock.EpochYear - creature.BirthYear;
        var raceLabel = creature.CreatureType.ToString().ToLowerInvariant();
        var professionLabel = creature.Profession?.ToString().ToLowerInvariant() ?? "wanderer";
        var affiliation = factionName != null ? $" and affiliated with {factionName}" : "";

        var sentences = new List<string>
        {
            $"A {age}-year-old {raceLabel} {professionLabel} of level {creature.Level}, hailing from "
                + $"{birthplaceName}{affiliation}.",
        };

        var familySentence = BuildFamilySentence(parentNames, childNames, siblingNames, spouse);
        if (familySentence != null)
        {
            sentences.Add(familySentence);
        }

        var workSentence = BuildWorkSentence(workplaceName, workJob, ownsWorkplace);
        if (workSentence != null)
        {
            sentences.Add(workSentence);
        }

        var daysOffSentence = BuildDaysOffSentence(daysOff);
        if (daysOffSentence != null)
        {
            sentences.Add(daysOffSentence);
        }

        if (homeName != null)
        {
            sentences.Add($"They live at {homeName}.");
        }

        var statSentence = BuildStatSentence(creature.Attributes);
        if (statSentence != null)
        {
            sentences.Add(statSentence);
        }

        var oceanSentence = BuildOceanSentence();
        if (oceanSentence != null)
        {
            sentences.Add(oceanSentence);
        }

        sentences.Add(PhysicalQuirks[Random.Shared.Next(PhysicalQuirks.Length)]);
        sentences.Add(
            string.Format(
                CultureInfo.InvariantCulture,
                SpeechPatternTemplates[Random.Shared.Next(SpeechPatternTemplates.Length)],
                birthplaceName
            )
        );
        sentences.Add(
            $"They enjoy {Hobbies[Random.Shared.Next(Hobbies.Length)]} in their spare time."
        );

        return string.Join(" ", sentences);
    }

    private static string? BuildStatSentence(Attributes attributes)
    {
        int[] values =
        [
            attributes.Strength,
            attributes.Defense,
            attributes.Dexterity,
            attributes.Endurance,
            attributes.Stamina,
            attributes.Mana,
            attributes.Intelligence,
        ];
        var mean = values.Average();
        var stdDev = StdDev(values.Select(v => (double)v));

        var highPhrases = new List<string>();
        var lowPhrases = new List<string>();
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] > mean + stdDev)
            {
                highPhrases.Add(StatDescriptors[i].HighPhrase);
            }
            else if (values[i] < mean - stdDev)
            {
                lowPhrases.Add(StatDescriptors[i].LowPhrase);
            }
        }

        if (highPhrases.Count == 0 && lowPhrases.Count == 0)
        {
            return null;
        }

        if (highPhrases.Count > 0 && lowPhrases.Count > 0)
        {
            return $"They are particularly {JoinPhrases(highPhrases)}, but {JoinPhrases(lowPhrases)}.";
        }

        return highPhrases.Count > 0
            ? $"They are particularly {JoinPhrases(highPhrases)}."
            : $"Despite their station, they are {JoinPhrases(lowPhrases)}.";
    }

    private static (Guid BuildingId, string Name)? ResolveBuilding(
        CreatureJob? job,
        IReadOnlyDictionary<Guid, Guid> buildingIdByRoomId,
        IReadOnlyDictionary<Guid, Building> buildingById
    )
    {
        if (job?.RoomId == null)
        {
            return null;
        }

        if (!buildingIdByRoomId.TryGetValue(job.RoomId.Value, out var buildingId))
        {
            return null;
        }

        return buildingById.TryGetValue(buildingId, out var building)
            ? (buildingId, building.Name)
            : null;
    }

    private static string? BuildWorkSentence(
        string? workplaceName,
        CreatureJob? workJob,
        bool ownsWorkplace
    )
    {
        if (workplaceName == null || workJob == null)
        {
            return null;
        }

        var hours = $"{FormatHour(workJob.StartHour)} to {FormatHour(workJob.EndHour)}";
        return ownsWorkplace
            ? $"They own {workplaceName}, where they typically work {hours}."
            : $"They work at {workplaceName}, typically {hours}.";
    }

    private static string FormatHour(int hour)
    {
        var period = hour < 12 ? "am" : "pm";
        var displayHour = hour % 12 == 0 ? 12 : hour % 12;
        return $"{displayHour}{period}";
    }

    private static string? BuildDaysOffSentence(DayOfWeek[]? daysOff)
    {
        if (daysOff is not { Length: > 0 })
        {
            return null;
        }

        var dayWord = daysOff.Length == 1 ? "day off is" : "days off are";
        return $"Their {dayWord} {JoinPhrases(daysOff.Select(GameClock.GetDayName).ToArray())}.";
    }

    private static string? BuildFamilySentence(
        IReadOnlyList<string>? parentNames,
        IReadOnlyList<string>? childNames,
        IReadOnlyList<string>? siblingNames,
        SpouseInfo? spouse
    )
    {
        var sentences = new List<string>();

        if (spouse != null)
        {
            sentences.Add($"Their {spouse.Label} is {spouse.Name}.");
        }

        if (parentNames is { Count: > 0 })
        {
            sentences.Add($"Their parents are {JoinPhrases(parentNames)}.");
        }

        if (childNames is { Count: > 0 })
        {
            var childWord = childNames.Count == 1 ? "child" : "children";
            sentences.Add(
                $"They have {CountWord(childNames.Count)} {childWord}: {JoinPhrases(childNames)}."
            );
        }

        if (siblingNames is { Count: > 0 })
        {
            var siblingWord = siblingNames.Count == 1 ? "sibling" : "siblings";
            sentences.Add(
                $"They have {CountWord(siblingNames.Count)} {siblingWord}: {JoinPhrases(siblingNames)}."
            );
        }

        return sentences.Count > 0 ? string.Join(" ", sentences) : null;
    }

    private static string CountWord(int count)
    {
        return count switch
        {
            1 => "one",
            2 => "two",
            3 => "three",
            4 => "four",
            5 => "five",
            _ => count.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static string? BuildOceanSentence()
    {
        var highPhrases = new List<string>();
        var lowPhrases = new List<string>();
        foreach (var (_, highPhrase, lowPhrase) in OceanDescriptors)
        {
            var roll = Random.Shared.Next(101);
            if (roll > 70)
            {
                highPhrases.Add(highPhrase);
            }
            else if (roll < 30)
            {
                lowPhrases.Add(lowPhrase);
            }
        }

        if (highPhrases.Count == 0 && lowPhrases.Count == 0)
        {
            return null;
        }

        if (highPhrases.Count > 0 && lowPhrases.Count > 0)
        {
            return $"They are {JoinPhrases(highPhrases)}, but {JoinPhrases(lowPhrases)}.";
        }

        var joined = highPhrases.Count > 0 ? JoinPhrases(highPhrases) : JoinPhrases(lowPhrases);
        return $"They are {joined}.";
    }

    private static string JoinPhrases(IReadOnlyList<string> phrases)
    {
        return phrases.Count switch
        {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}",
        };
    }

    private static double StdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        var mean = list.Average();
        var variance = list.Average(v => (v - mean) * (v - mean));
        return Math.Sqrt(variance);
    }
}
