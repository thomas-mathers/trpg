using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

public static class FactionNames
{
    public const string HollowCoin = "The Hollow Coin";
    public const string CrimsonVow = "The Crimson Vow";
    public const string Shieldsworn = "The Shieldsworn";
    public const string ArcaneConclave = "The Arcane Conclave";
    public const string IronhideKin = "The Ironhide Kin";
    public const string RedTalon = "The Red Talon";
    public const string SilverVigil = "The Silver Vigil";
    public const string CinderPact = "The Cinder Pact";
    public const string NightboundCourt = "The Nightbound Court";
    public const string AshwoodPack = "The Ashwood Pack";
    public const string Reclaimers = "The Reclaimers";
    public const string HouseAshvale = "House Ashvale";
    public const string HouseMarrow = "House Marrow";
    public const string BrokenToll = "The Broken Toll";
}

public record FactionRoster(
    IReadOnlyList<Faction> JoinableFactions,
    IReadOnlyList<Faction> AntagonistFactions,
    Faction BrokenToll
);

public static class FactionRosterGenerator
{
    public static FactionRoster Generate(Guid worldId) =>
        new(
            [
                Make(
                    worldId,
                    FactionNames.HollowCoin,
                    "Thieves, smugglers, and fences moving contraband through the underworld.",
                    FactionKind.Joinable
                ),
                Make(
                    worldId,
                    FactionNames.CrimsonVow,
                    "Contract killers bound by blood oath and loyal to whoever pays.",
                    FactionKind.Joinable
                ),
                Make(
                    worldId,
                    FactionNames.Shieldsworn,
                    "A mercenary order bound by an old code and guarding a secret of its own.",
                    FactionKind.Joinable
                ),
                Make(
                    worldId,
                    FactionNames.ArcaneConclave,
                    "A scholarly order that studies and regulates magic.",
                    FactionKind.Joinable
                ),
                Make(
                    worldId,
                    FactionNames.IronhideKin,
                    "A proud and insular clan society wary of outsiders.",
                    FactionKind.Joinable
                ),
            ],
            [
                Make(
                    worldId,
                    FactionNames.RedTalon,
                    "A violent criminal gang competing for underworld territory.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.SilverVigil,
                    "A zealous order that hunts supernatural threats.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.CinderPact,
                    "Expelled mages who embraced forbidden magic.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.NightboundCourt,
                    "A secretive vampiric bloodline hidden within high society.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.AshwoodPack,
                    "A territorial werewolf pack at civilization's edge.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.Reclaimers,
                    "A displaced people waging resistance to reclaim their land.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.HouseAshvale,
                    "A noble dynasty competing for influence and favor.",
                    FactionKind.Antagonist
                ),
                Make(
                    worldId,
                    FactionNames.HouseMarrow,
                    "A rival noble dynasty competing for influence and favor.",
                    FactionKind.Antagonist
                ),
            ],
            MakeBrokenToll(
                worldId,
                FactionNames.BrokenToll,
                "Human raiders who prey on trade roads."
            )
        );

    private static Faction MakeBrokenToll(Guid worldId, string name, string description) =>
        new()
        {
            WorldId = worldId,
            Name = name,
            Description = description,
            Kind = FactionKind.Wilderness,
            CreatureType = CreatureType.Human,
            EncounterApproach = EncounterApproach.Shakedown,
            Aggression = 100,
            ReputationSensitivity = 50,
            RiskAversion = 35,
            Temperament = FactionTemperament.Predatory,
        };

    private static Faction Make(
        Guid worldId,
        string name,
        string description,
        FactionKind kind,
        CreatureType? creatureType = null
    ) =>
        new()
        {
            WorldId = worldId,
            Name = name,
            Description = description,
            Kind = kind,
            CreatureType = creatureType,
            Aggression = 0,
            ReputationSensitivity = 0,
            RiskAversion = 0,
            Temperament = FactionTemperament.Authoritative,
        };
}

public static class FactionStandingGenerator
{
    public static IReadOnlyList<FactionStanding> Generate(
        Guid worldId,
        IReadOnlyCollection<Faction> factions
    )
    {
        var byName = factions.ToDictionary(faction => faction.Name, StringComparer.Ordinal);
        var standings = new List<FactionStanding>();

        AddByName(FactionNames.HollowCoin, FactionNames.RedTalon, -70);
        AddByName(FactionNames.Shieldsworn, FactionNames.SilverVigil, -55);
        AddByName(FactionNames.ArcaneConclave, FactionNames.CinderPact, -80);
        AddByName(FactionNames.SilverVigil, FactionNames.NightboundCourt, -90);
        AddByName(FactionNames.SilverVigil, FactionNames.AshwoodPack, -75);
        AddByName(FactionNames.HouseAshvale, FactionNames.HouseMarrow, -65);

        foreach (var guard in factions.Where(faction => faction.Kind == FactionKind.CityGuard))
        {
            AddPair(guard, byName[FactionNames.HollowCoin], -35);
            AddPair(guard, byName[FactionNames.CrimsonVow], -30);
            AddPair(guard, byName[FactionNames.NightboundCourt], -60);
            AddPair(guard, byName[FactionNames.AshwoodPack], -50);
            AddPair(guard, byName[FactionNames.Reclaimers], -45);
        }

        foreach (var castle in factions.Where(faction => faction.Kind == FactionKind.Castle))
        {
            AddPair(castle, byName[FactionNames.CrimsonVow], -25);
            AddPair(castle, byName[FactionNames.Reclaimers], -65);
            AddPair(castle, byName[FactionNames.HouseMarrow], -35);
        }

        foreach (var criminal in new[] { FactionNames.HollowCoin, FactionNames.RedTalon })
        {
            AddByName(FactionNames.CrimsonVow, criminal, -20);
        }

        return standings;

        void AddByName(string leftName, string rightName, int score) =>
            AddPair(byName[leftName], byName[rightName], score);

        void AddPair(Faction left, Faction right, int score)
        {
            standings.Add(
                new FactionStanding
                {
                    WorldId = worldId,
                    FactionId = left.Id,
                    OtherFactionId = right.Id,
                    Score = score,
                }
            );
            standings.Add(
                new FactionStanding
                {
                    WorldId = worldId,
                    FactionId = right.Id,
                    OtherFactionId = left.Id,
                    Score = score,
                }
            );
        }
    }
}
