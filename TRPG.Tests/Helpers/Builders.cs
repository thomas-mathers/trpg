using TRPG.Application.Combat;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Data.Models;
using Profession = TRPG.Data.Models.Profession;

namespace TRPG.Tests.Helpers;

internal static class Builders
{
    public static StatFormulas MakeStatFormulas(CreatureGeneratorOptions? options = null) =>
        new(
            new TestOptionsSnapshot<CreatureGeneratorOptions>(
                options ?? new CreatureGeneratorOptions()
            )
        );

    public static Combatant MakeCombatant(
        Guid? creatureId = null,
        string name = "Test Combatant",
        bool isPlayer = true,
        int currentHp = 100,
        int currentAp = 20,
        int currentMp = 10
    ) =>
        new()
        {
            CreatureId = creatureId ?? Guid.NewGuid(),
            Name = name,
            IsPlayer = isPlayer,
            Level = 1,
            Attributes = MakeAttributes(),
            Abilities = [],
            Gold = 0,
            CurrentHp = currentHp,
            CurrentAp = currentAp,
            CurrentMp = currentMp,
        };

    public static CombatantBuilder NewCombatant() => new();

    public static Creature MakeCreature(
        Guid? worldId = null,
        CreatureType creatureType = CreatureType.Human,
        Profession? profession = Profession.Knight,
        Guid? birthStateId = null,
        Guid? stateId = null,
        Guid? cityId = null,
        Guid? districtId = null,
        Guid? roomId = null,
        int birthYear = 1000,
        string name = "Test Creature",
        int level = 1,
        Attributes? baseAttributes = null,
        int? currentHp = null,
        int? currentAp = null,
        int? currentMp = null
    )
    {
        var attributes = baseAttributes ?? MakeAttributes();

        return new Creature
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name,
            CreatureType = creatureType,
            BirthStateId = birthStateId ?? Guid.NewGuid(),
            BirthYear = birthYear,
            Profession = profession,
            StateId = stateId ?? Guid.NewGuid(),
            CityId = cityId,
            DistrictId = districtId,
            RoomId = roomId,
            Level = level,
            BaseAttributes = attributes,
            CurrentHp = currentHp ?? attributes.MaximumHp,
            CurrentAp = currentAp ?? attributes.MaximumAp,
            CurrentMp = currentMp ?? attributes.MaximumMp,
            Strength = attributes.Strength,
            Dexterity = attributes.Dexterity,
            Intelligence = attributes.Intelligence,
            Endurance = attributes.Endurance,
            Stamina = attributes.Stamina,
            Mana = attributes.Mana,
            Defense = attributes.Defense,
            MaximumHp = attributes.MaximumHp,
            MaximumAp = attributes.MaximumAp,
            MaximumMp = attributes.MaximumMp,
            MovementSpeed = attributes.MovementSpeed,
            PhysicalResistance = attributes.PhysicalResistance,
            FireResistance = attributes.FireResistance,
            IceResistance = attributes.IceResistance,
            LightningResistance = attributes.LightningResistance,
            PoisonResistance = attributes.PoisonResistance,
            MagicResistance = attributes.MagicResistance,
        };
    }

    public static Attributes MakeAttributes()
    {
        var baseAttributes = new Attributes
        {
            Strength = 10,
            Defense = 5,
            Dexterity = 8,
            Endurance = 7,
            Stamina = 6,
            Mana = 4,
            Intelligence = 9,
        };

        var statFormulas = MakeStatFormulas();
        return baseAttributes with
        {
            MaximumHp = statFormulas.CalculateMaximumHp(baseAttributes),
            MaximumAp = statFormulas.CalculateMaximumAp(baseAttributes),
            MaximumMp = statFormulas.CalculateMaximumMp(baseAttributes),
        };
    }

    public static Item MakeItem(Guid? worldId = null, ItemRarity rarity = ItemRarity.Normal)
    {
        return new Item
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test item",
            Weight = 1,
            GoldValue = 10,
            Rarity = rarity,
        };
    }

    public static WeaponItem MakeWeaponItem(
        Guid? worldId = null,
        WeaponType type = WeaponType.Sword
    )
    {
        return new WeaponItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon",
            Weight = 8,
            GoldValue = 50,
            Type = type,
            MinDamage = 5,
            MaxDamage = 15,
            Range = 1,
            AttackSpeed = 7,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
        };
    }

    public static ArmorItem MakeArmorItem(Guid? worldId = null, ArmorType type = ArmorType.Chest)
    {
        return new ArmorItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test armor",
            Weight = 15,
            GoldValue = 40,
            Type = type,
            Defense = 10,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
        };
    }

    public static ShieldItem MakeShieldItem(Guid? worldId = null)
    {
        return new ShieldItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test shield",
            Weight = 8,
            GoldValue = 30,
            Defense = 8,
            BlockChance = 0.25f,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
        };
    }

    public static ConsumableItem MakeConsumableItem(Guid? worldId = null)
    {
        return new ConsumableItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test consumable",
            Weight = 1,
            GoldValue = 10,
            Resource = ResourceType.Hp,
            Amount = 50,
            Duration = 0,
        };
    }

    public static AmmunitionItem MakeAmmunitionItem(
        Guid? worldId = null,
        AmmoType type = AmmoType.Arrow
    )
    {
        return new AmmunitionItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test ammo",
            Weight = 2,
            GoldValue = 5,
            Type = type,
        };
    }

    public static AccessoryItem MakeAccessoryItem(
        Guid? worldId = null,
        AccessoryType type = AccessoryType.Ring
    )
    {
        return new AccessoryItem
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test accessory",
            Weight = 1,
            GoldValue = 20,
            Type = type,
        };
    }

    public static Quest MakeQuest(Guid giverId, Guid? worldId = null)
    {
        return new Quest
        {
            WorldId = worldId ?? Guid.NewGuid(),
            GiverId = giverId,
            Name = $"Quest-{Guid.NewGuid():N}",
            Description = "A test quest",
            GoldReward = 100,
            ExperienceReward = 50,
        };
    }

    public static Faction MakeFaction(Guid? worldId = null)
    {
        return new Faction
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Faction-{Guid.NewGuid():N}",
            Description = "A test faction",
        };
    }

    public static World MakeWorld()
    {
        return new World
        {
            Name = $"World-{Guid.NewGuid():N}",
            Description = "A test world",
            Boundary = new Rectangle(0, 0, 10000, 10000),
        };
    }

    public static Country MakeCountry(Guid worldId)
    {
        return new Country
        {
            WorldId = worldId,
            Name = $"Country-{Guid.NewGuid():N}",
            Description = "A test country",
            Boundary = new Polygon
            {
                Points =
                [
                    new Point(0, 0),
                    new Point(3000, 0),
                    new Point(3000, 3000),
                    new Point(0, 3000),
                ],
            },
        };
    }

    public static State MakeState(Guid countryId, Guid? worldId = null)
    {
        return new State
        {
            CountryId = countryId,
            Name = $"State-{Guid.NewGuid():N}",
            Description = "A test state",
            Width = 100,
            Height = 100,
            Center = new Point(50, 50),
            Boundary = new Polygon
            {
                Points =
                [
                    new Point(0, 0),
                    new Point(100, 0),
                    new Point(100, 100),
                    new Point(0, 100),
                ],
            },
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static City MakeCity(
        Guid stateId,
        Guid countryId,
        bool isCapital = false,
        Guid? worldId = null,
        string? name = null
    )
    {
        return new City
        {
            StateId = stateId,
            CountryId = countryId,
            Name = name ?? $"City-{Guid.NewGuid():N}",
            Description = "A test city",
            IsCapital = isCapital,
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static District MakeDistrict(
        Guid cityId,
        DistrictType districtType = DistrictType.CityCenter,
        Guid? worldId = null
    )
    {
        return new District
        {
            CityId = cityId,
            DistrictType = districtType,
            Name = $"District-{Guid.NewGuid():N}",
            Description = "A test district",
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static Room MakeRoom(Guid buildingId, int capacity = 4, Guid? worldId = null)
    {
        return new Room
        {
            BuildingId = buildingId,
            Capacity = capacity,
            Name = $"Room-{Guid.NewGuid():N}",
            Description = "A test room",
            FloorNumber = 0,
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static Building MakeBuilding(
        Guid stateId,
        Guid? cityId = null,
        Guid? districtId = null,
        Guid? worldId = null,
        string? name = null,
        BuildingType buildingType = BuildingType.House
    )
    {
        return new Building
        {
            StateId = stateId,
            CityId = cityId,
            DistrictId = districtId,
            Name = name ?? $"Building-{Guid.NewGuid():N}",
            Description = "A test building",
            BuildingType = buildingType,
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static CreatureJob MakeCreatureJob(
        Guid creatureId,
        int priority = 1,
        CreatureJobAction action = CreatureJobAction.Idle,
        int startHour = 8,
        int endHour = 17,
        Guid? roomId = null,
        Guid? worldId = null,
        DayOfWeek? specificDay = null
    )
    {
        return new CreatureJob
        {
            CreatureId = creatureId,
            Action = action,
            StartHour = startHour,
            EndHour = endHour,
            SpecificDay = specificDay,
            Priority = priority,
            StateId = Guid.NewGuid(),
            RoomId = roomId,
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static WorldEvent MakeWorldEvent(Guid worldId, Guid? stateId = null)
    {
        return new WorldEvent
        {
            WorldId = worldId,
            Description = "A test world event",
            Date = DateTime.UtcNow,
            Tags = [],
            StateId = stateId,
        };
    }

    public static GameSession MakeGameSession(
        Guid worldId,
        Guid playerId,
        TimeSpan playtime = default
    )
    {
        return new GameSession
        {
            WorldId = worldId,
            PlayerId = playerId,
            Playtime = playtime,
        };
    }

    public static Fight MakeFight(
        Guid worldId,
        Guid playerId,
        IReadOnlyList<Guid> combatantIds,
        DateTime? startedAt = null
    )
    {
        return new Fight
        {
            WorldId = worldId,
            PlayerId = playerId,
            CombatantIds = combatantIds,
            StartedAt = startedAt ?? DateTime.UtcNow,
        };
    }
}
