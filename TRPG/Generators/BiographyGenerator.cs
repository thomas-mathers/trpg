using System.Globalization;
using TRPG.Models;

namespace TRPG.Generators;

internal record BiographyGeneratorInput(
    IReadOnlyList<Creature> Creatures,
    IReadOnlyDictionary<Guid, State> StateById,
    IReadOnlyList<FactionMember> FactionMembers,
    IReadOnlyList<Faction> Factions,
    IReadOnlySet<Guid> CityFactionIds);

internal static class BiographyGenerator {
    private static readonly (string Name, string HighPhrase, string LowPhrase)[] StatDescriptors = [
        ("Strength", "strong", "frail"),
        ("Defense", "tough", "easily wounded"),
        ("Dexterity", "nimble", "clumsy"),
        ("Endurance", "tireless", "easily winded"),
        ("Stamina", "energetic", "listless"),
        ("Mana", "attuned to magic", "utterly unmagical"),
        ("Intelligence", "sharp-witted", "plainspoken")
    ];

    private static readonly (string Trait, string HighPhrase, string LowPhrase)[] OceanDescriptors = [
        ("Openness", "endlessly curious about new ideas", "set in their ways"),
        ("Conscientiousness", "disciplined", "easily distracted"),
        ("Extraversion", "gregarious", "reserved, saying little unless asked"),
        ("Agreeableness", "quick to trust", "blunt, sometimes to the point of rudeness"),
        ("Neuroticism", "prone to overthinking", "unshakably calm, rarely rattled")
    ];

    private static readonly string[] Hobbies = [
        "woodworking", "chess", "gardening", "storytelling", "fishing", "collecting old coins",
        "sketching", "brewing", "birdwatching", "poetry", "whittling", "foraging", "stargazing",
        "dice games", "herbalism", "singing", "dancing", "tinkering", "falconry", "cartography"
    ];

    private static readonly string[] PhysicalQuirks = [
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
        "Their hands have a soft, well-worn look about them."
    ];

    private static readonly string[] SpeechPatternTemplates = [
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
        "They tend to wander into long, winding tangents when they speak."
    ];

    private record GoldStats(double Mean, double StdDev);

    public static void AssignBiographies(BiographyGeneratorInput input) {
        var goldStatsByLevel = input.Creatures
            .GroupBy(c => c.Level)
            .ToDictionary(g => g.Key, g => {
                var goldValues = g.Select(c => (double) c.Gold).ToArray();
                return new GoldStats(goldValues.Average(), StdDev(goldValues));
            });

        var factionById = input.Factions.ToDictionary(f => f.Id);
        var factionNameByCreatureId = input.FactionMembers
            .Where(fm => !input.CityFactionIds.Contains(fm.FactionId))
            .GroupBy(fm => fm.CreatureId)
            .ToDictionary(g => g.Key, g => factionById[g.First().FactionId].Name);

        foreach (var creature in input.Creatures) {
            var goldStats = goldStatsByLevel[creature.Level];
            factionNameByCreatureId.TryGetValue(creature.Id, out var factionName);
            var birthplaceName = input.StateById[creature.BirthStateId].Name;
            creature.Biography = BuildBiography(creature, birthplaceName, factionName, goldStats);
        }
    }

    private static string BuildBiography(Creature creature, string birthplaceName, string? factionName,
        GoldStats goldStats) {
        var age = GameClock.EpochYear - creature.BirthYear;
        var raceLabel = creature.CreatureType.ToString().ToLowerInvariant();
        var professionLabel = creature.Profession?.ToString().ToLowerInvariant() ?? "wanderer";
        var affiliation = factionName != null ? $" and affiliated with {factionName}" : "";

        var sentences = new List<string> {
            $"A {age}-year-old {raceLabel} {professionLabel} of level {creature.Level}, hailing from " +
            $"{birthplaceName}{affiliation}."
        };

        var statSentence = BuildStatSentence(creature.Attributes);
        if (statSentence != null) {
            sentences.Add(statSentence);
        }

        var oceanSentence = BuildOceanSentence();
        if (oceanSentence != null) {
            sentences.Add(oceanSentence);
        }

        var goldSentence = BuildGoldSentence(creature.Gold, goldStats);
        if (goldSentence != null) {
            sentences.Add(goldSentence);
        }

        sentences.Add(PhysicalQuirks[Random.Shared.Next(PhysicalQuirks.Length)]);
        sentences.Add(string.Format(CultureInfo.InvariantCulture,
            SpeechPatternTemplates[Random.Shared.Next(SpeechPatternTemplates.Length)], birthplaceName));
        sentences.Add($"They enjoy {Hobbies[Random.Shared.Next(Hobbies.Length)]} in their spare time.");

        return string.Join(" ", sentences);
    }

    private static string? BuildStatSentence(Attributes attributes) {
        int[] values = [
            attributes.Strength, attributes.Defense, attributes.Dexterity, attributes.Endurance,
            attributes.Stamina, attributes.Mana, attributes.Intelligence
        ];
        var mean = values.Average();
        var stdDev = StdDev(values.Select(v => (double) v));

        var highPhrases = new List<string>();
        var lowPhrases = new List<string>();
        for (var i = 0; i < values.Length; i++) {
            if (values[i] > mean + stdDev) {
                highPhrases.Add(StatDescriptors[i].HighPhrase);
            }
            else if (values[i] < mean - stdDev) {
                lowPhrases.Add(StatDescriptors[i].LowPhrase);
            }
        }

        if (highPhrases.Count == 0 && lowPhrases.Count == 0) {
            return null;
        }

        if (highPhrases.Count > 0 && lowPhrases.Count > 0) {
            return $"They are particularly {JoinPhrases(highPhrases)}, but {JoinPhrases(lowPhrases)}.";
        }

        return highPhrases.Count > 0
            ? $"They are particularly {JoinPhrases(highPhrases)}."
            : $"Despite their station, they are {JoinPhrases(lowPhrases)}.";
    }

    private static string? BuildOceanSentence() {
        var highPhrases = new List<string>();
        var lowPhrases = new List<string>();
        foreach (var (_, highPhrase, lowPhrase) in OceanDescriptors) {
            var roll = Random.Shared.Next(101);
            if (roll > 70) {
                highPhrases.Add(highPhrase);
            }
            else if (roll < 30) {
                lowPhrases.Add(lowPhrase);
            }
        }

        if (highPhrases.Count == 0 && lowPhrases.Count == 0) {
            return null;
        }

        if (highPhrases.Count > 0 && lowPhrases.Count > 0) {
            return $"They are {JoinPhrases(highPhrases)}, but {JoinPhrases(lowPhrases)}.";
        }

        var joined = highPhrases.Count > 0 ? JoinPhrases(highPhrases) : JoinPhrases(lowPhrases);
        return $"They are {joined}.";
    }

    private static string? BuildGoldSentence(int gold, GoldStats goldStats) {
        if (gold > goldStats.Mean + goldStats.StdDev) {
            return "They are notably wealthy for someone of their station.";
        }

        if (gold < goldStats.Mean - goldStats.StdDev) {
            return "They scrape by with less coin than most in their position.";
        }

        return null;
    }

    private static string JoinPhrases(IReadOnlyList<string> phrases) {
        return phrases.Count switch {
            1 => phrases[0],
            2 => $"{phrases[0]} and {phrases[1]}",
            _ => $"{string.Join(", ", phrases.Take(phrases.Count - 1))}, and {phrases[^1]}"
        };
    }

    private static double StdDev(IEnumerable<double> values) {
        var list = values.ToList();
        var mean = list.Average();
        var variance = list.Average(v => (v - mean) * (v - mean));
        return Math.Sqrt(variance);
    }
}
