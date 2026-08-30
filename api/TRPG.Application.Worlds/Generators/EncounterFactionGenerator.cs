using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Generators;

internal static class EncounterFactionGenerator
{
    public static IReadOnlyDictionary<CreatureType, Faction> Generate(Guid worldId)
    {
        return new Dictionary<CreatureType, Faction>
        {
            [CreatureType.Beast] = MakeFaction(
                worldId,
                CreatureType.Beast,
                "Wild Beasts",
                "Predators and territorial animals that defend their hunting grounds.",
                FactionTemperament.Territorial,
                aggression: 60,
                reputationSensitivity: 0,
                riskAversion: 40
            ),
            [CreatureType.Goblin] = MakeFaction(
                worldId,
                CreatureType.Goblin,
                "Goblin Raiders",
                "Scavengers and raiders who prey on isolated travelers.",
                FactionTemperament.Predatory,
                aggression: 70,
                reputationSensitivity: 40,
                riskAversion: 60
            ),
            [CreatureType.Undead] = MakeFaction(
                worldId,
                CreatureType.Undead,
                "Restless Dead",
                "The dead who attack the living without fear or restraint.",
                FactionTemperament.Fanatical,
                aggression: 100,
                reputationSensitivity: 0,
                riskAversion: 0
            ),
            [CreatureType.Wraith] = MakeFaction(
                worldId,
                CreatureType.Wraith,
                "Restless Wraiths",
                "Malignant spirits who attack the living without fear or restraint.",
                FactionTemperament.Fanatical,
                aggression: 100,
                reputationSensitivity: 0,
                riskAversion: 0
            ),
            [CreatureType.Construct] = MakeFaction(
                worldId,
                CreatureType.Construct,
                "Ancient Constructs",
                "Forgotten guardians bound to defend the places they were built to protect.",
                FactionTemperament.Fanatical,
                aggression: 80,
                reputationSensitivity: 0,
                riskAversion: 10
            ),
            [CreatureType.Demon] = MakeFaction(
                worldId,
                CreatureType.Demon,
                "Infernal Host",
                "Malevolent creatures who see mortal travelers as prey.",
                FactionTemperament.Predatory,
                aggression: 90,
                reputationSensitivity: 10,
                riskAversion: 20
            ),
            [CreatureType.Giant] = MakeFaction(
                worldId,
                CreatureType.Giant,
                "Giant Clans",
                "Proud giants who defend their domains from intruders.",
                FactionTemperament.Territorial,
                aggression: 65,
                reputationSensitivity: 20,
                riskAversion: 45
            ),
            [CreatureType.Dragon] = MakeFaction(
                worldId,
                CreatureType.Dragon,
                "Dragons",
                "Ancient predators who rule the territory around their lairs.",
                FactionTemperament.Territorial,
                aggression: 85,
                reputationSensitivity: 5,
                riskAversion: 35
            ),
            [CreatureType.Elemental] = MakeFaction(
                worldId,
                CreatureType.Elemental,
                "Elemental Forces",
                "Unstable elemental beings that lash out at intruders.",
                FactionTemperament.Fanatical,
                aggression: 75,
                reputationSensitivity: 0,
                riskAversion: 10
            ),
        };
    }

    private static Faction MakeFaction(
        Guid worldId,
        CreatureType creatureType,
        string name,
        string description,
        FactionTemperament temperament,
        int aggression,
        int reputationSensitivity,
        int riskAversion
    ) =>
        new()
        {
            WorldId = worldId,
            CreatureType = creatureType,
            Name = name,
            Description = description,
            Temperament = temperament,
            Aggression = aggression,
            ReputationSensitivity = reputationSensitivity,
            RiskAversion = riskAversion,
        };
}
