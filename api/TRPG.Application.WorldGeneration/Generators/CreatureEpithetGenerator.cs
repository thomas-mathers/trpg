namespace TRPG.Application.WorldGeneration.Generators;

// Distinguishes a specific creature as a named quest target from any other same-species spawn —
// deliberately separate from CreatureGenerator's baseline name pools, which carry no implication
// of notoriety.
public static class CreatureEpithetGenerator
{
    private static readonly string[] Epithets =
    [
        "the Butcher",
        "the Cruel",
        "the Ravager",
        "Bloodfang",
        "the Merciless",
        "Skullcrusher",
        "the Grim",
        "the Wicked",
        "Ironjaw",
        "the Despoiler",
        "the Unforgiving",
        "Direfang",
        "the Bonebreaker",
        "the Vile",
        "Gravemaw",
    ];

    public static string ComposeName(string baseName, Random random) =>
        $"{baseName} {Epithets[random.Next(Epithets.Length)]}";
}
