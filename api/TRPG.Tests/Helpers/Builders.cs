using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Results;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;
using Profession = TRPG.Domain.Models.Profession;

namespace TRPG.Tests.Helpers;

internal static class Builders
{
    public static CreatureGenerator MakeCreatureGenerator(CreatureGeneratorOptions? options = null)
    {
        var itemGenerator = new ItemGenerator(
            new WeaponGenerator(),
            new ArmorGenerator(),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        );
        return new CreatureGenerator(
            itemGenerator,
            new TestOptionsSnapshot<CreatureGeneratorOptions>(
                options ?? new CreatureGeneratorOptions()
            )
        );
    }

    public static Combatant MakeCombatant(
        Guid? creatureId = null,
        string name = "Test Combatant",
        bool isPlayer = true,
        CreatureType creatureType = CreatureType.Human,
        int currentHp = 100,
        int currentAp = 20,
        int currentMp = 10,
        CombatOptions? combatOptions = null
    ) =>
        new()
        {
            CreatureId = creatureId ?? Guid.NewGuid(),
            Name = name,
            IsPlayer = isPlayer,
            CreatureType = creatureType,
            Level = 1,
            Attributes = MakeAttributes(),
            Abilities = [],
            NaturalWeaponMinDamage = 3,
            NaturalWeaponMaxDamage = 3,
            CurrentHp = currentHp,
            CurrentAp = currentAp,
            CurrentMp = currentMp,
            CombatOptions = combatOptions ?? new CombatOptions(),
        };

    public static CombatantBuilder NewCombatant() => new();

    public static InGameDate MakeDate(int hour) =>
        new(975, "Thawmoon", 1, "Stormday", DayOfWeek.Thursday, hour);

    public static LocationConnector MakeLocationConnector(
        Guid originLocationId,
        Guid? destinationLocationId = null,
        Guid? worldId = null,
        string name = "Door",
        string description = "A door.",
        string destinationLabel = "Outside"
    ) =>
        new()
        {
            OriginLocationId = originLocationId,
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name,
            Description = description,
            DestinationLocationId = destinationLocationId ?? Guid.NewGuid(),
            DestinationLabel = destinationLabel,
        };

    public static DoorConnector MakeDoorConnector(
        Guid connectorId,
        bool isLocked = false,
        Guid? worldId = null
    ) =>
        new()
        {
            ConnectorId = connectorId,
            IsLocked = isLocked,
            WorldId = worldId ?? Guid.NewGuid(),
        };

    public static CombatantResult MakeCombatantState(
        Guid id,
        string name,
        bool isPlayer,
        int currentHp,
        bool isAlive,
        IReadOnlyDictionary<Guid, int>? itemsUsedCounts = null
    ) =>
        new CombatantResult(
            Id: id,
            Name: name,
            Level: 1,
            IsPlayer: isPlayer,
            CurrentHp: currentHp,
            MaximumHp: 100,
            CurrentAp: 7,
            MaximumAp: 10,
            CurrentMp: 2,
            MaximumMp: 5,
            IsAlive: isAlive,
            Abilities: [],
            ActiveConditions: new Dictionary<ConditionType, int>(),
            ActiveDots: [],
            ActiveHots: [],
            ActiveBuffs: [],
            ItemsUsedCounts: itemsUsedCounts ?? new Dictionary<Guid, int>()
        );

    public static CombatState MakeCombatState(
        CombatOutcome outcome,
        IReadOnlyList<CombatantResult> combatants,
        int? goldLooted = null,
        IReadOnlyDictionary<WeaponType, int>? weaponSwingCounts = null,
        IReadOnlyList<CombatResolution>? events = null
    ) =>
        new(
            Outcome: outcome,
            Combatants: combatants,
            Events: events ?? [],
            WeaponSwingCounts: weaponSwingCounts ?? new Dictionary<WeaponType, int>(),
            SkillUsageCounts: new Dictionary<Skill, int>()
        );

    public static AttackAbility MakeAttackAbility(
        DamageType damageType = DamageType.Physical,
        float damageAmount = 100
    ) =>
        new()
        {
            Name = "Test Attack",
            Description = "A test attack.",
            TargetType = AttackTargetType.Single,
            DamageType = damageType,
            DamageAmount = damageAmount,
            DamageAmountType =
                damageType == DamageType.Physical ? AmountType.Percent : AmountType.Flat,
        };

    public static SupportAbility MakeInstantHealAbility(
        string name = "Cure",
        int amount = 20,
        int cost = 0,
        int cooldown = 0
    ) =>
        new()
        {
            Name = name,
            Description = "A test instant-heal ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = TargetType.Single,
            HealAmount = amount,
        };

    public static SupportAbility MakeBuffAbility(
        string name = "Buff",
        int cost = 0,
        int cooldown = 0,
        int duration = 3,
        TargetType targetType = TargetType.Single,
        AttributeName attribute = AttributeName.Strength,
        float amount = 5f,
        AmountType amountType = AmountType.Flat
    ) =>
        new()
        {
            Name = name,
            Description = "A test support ability.",
            ApCost = cost,
            Cooldown = cooldown,
            TargetType = targetType,
            Buffs =
            [
                new AttributeEffect
                {
                    Attribute = attribute,
                    AmountType = amountType,
                    Amount = amount,
                    Duration = duration,
                },
            ],
        };

    public static Creature MakeCreature(
        Guid? worldId = null,
        CreatureType creatureType = CreatureType.Human,
        Profession? profession = Profession.Knight,
        Guid? birthLocationId = null,
        Guid? locationId = null,
        int birthYear = 1000,
        string name = "Test Creature",
        int level = 1,
        Attributes? baseAttributes = null,
        int? currentHp = null,
        int? currentAp = null,
        int? currentMp = null,
        CreatureState state = default,
        int naturalWeaponMinDamage = 3,
        int naturalWeaponMaxDamage = 3
    )
    {
        var attributes = baseAttributes ?? MakeAttributes();

        return new Creature
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name,
            CreatureType = creatureType,
            BirthLocationId = birthLocationId ?? Guid.NewGuid(),
            BirthYear = birthYear,
            Profession = profession,
            LocationId = locationId ?? Guid.NewGuid(),
            Level = level,
            State = state,
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
            NaturalWeaponMinDamage = naturalWeaponMinDamage,
            NaturalWeaponMaxDamage = naturalWeaponMaxDamage,
        };
    }

    public static Container MakeContainer(Guid? worldId = null, Guid? locationId = null) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Container-{Guid.NewGuid():N}",
            Description = "A test container",
            LocationId = locationId ?? Guid.NewGuid(),
        };

    public static Workstation MakeWorkstation(
        Guid? worldId = null,
        Guid? locationId = null,
        Guid? occupantId = null
    ) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Workstation-{Guid.NewGuid():N}",
            Description = "A test workstation",
            LocationId = locationId ?? Guid.NewGuid(),
            WorkstationType = WorkstationType.Trade,
            OccupantId = occupantId,
        };

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

        var options = new CreatureGeneratorOptions();
        return baseAttributes with
        {
            MaximumHp = StatFormulas.CalculateMaximumHp(baseAttributes, options),
            MaximumAp = StatFormulas.CalculateMaximumAp(baseAttributes, options),
            MaximumMp = StatFormulas.CalculateMaximumMp(baseAttributes, options),
        };
    }

    public static Item MakeItem(Guid? worldId = null)
    {
        return new Item
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test item",
            Weight = 1,
        };
    }

    public static Gold MakeGold(Guid? worldId = null, int quantity = 0)
    {
        return new Gold
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = "Gold",
            Quantity = quantity,
        };
    }

    public static Weapon MakeWeaponItem(
        Guid? worldId = null,
        WeaponType type = WeaponType.Sword,
        int minDamage = 5,
        int maxDamage = 15,
        int attacksPerTurn = 1,
        bool isTwoHanded = false,
        int quantity = 0,
        IReadOnlyCollection<ItemModifier>? modifiers = null
    )
    {
        return new Weapon
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon",
            Weight = 8,
            Quantity = quantity,
            GoldValue = 50,
            Type = type,
            MinDamage = minDamage,
            MaxDamage = maxDamage,
            Range = 1,
            AttacksPerTurn = attacksPerTurn,
            IsTwoHanded = isTwoHanded,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
            Modifiers = modifiers ?? [],
        };
    }

    public static Armor MakeArmorItem(
        Guid? worldId = null,
        ArmorType type = ArmorType.Chest,
        int quantity = 0,
        IReadOnlyCollection<ItemModifier>? modifiers = null
    )
    {
        return new Armor
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test armor",
            Weight = 15,
            Quantity = quantity,
            GoldValue = 40,
            Type = type,
            Defense = 10,
            DurabilityMax = 100,
            DurabilityCurrent = 100,
            Modifiers = modifiers ?? [],
        };
    }

    public static Shield MakeShieldItem(
        Guid? worldId = null,
        float blockChance = 0.25f,
        IReadOnlyCollection<ItemModifier>? modifiers = null
    )
    {
        return new Shield
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test shield",
            Weight = 8,
            GoldValue = 30,
            Defense = 8,
            BlockChance = blockChance,
            Modifiers = modifiers ?? [],
            DurabilityMax = 100,
            DurabilityCurrent = 100,
        };
    }

    public static Consumable MakeConsumableItem(
        Guid? worldId = null,
        string? name = null,
        ResourceType resource = ResourceType.Hp,
        int amount = 50
    )
    {
        return new Consumable
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name ?? $"Item-{Guid.NewGuid():N}",
            Description = "A test consumable",
            Weight = 1,
            GoldValue = 10,
            Resource = resource,
            RestoreAmount = amount,
            Duration = 0,
        };
    }

    public static Ammunition MakeAmmunitionItem(
        Guid? worldId = null,
        AmmoType type = AmmoType.Arrow
    )
    {
        return new Ammunition
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test ammo",
            Weight = 2,
            GoldValue = 5,
            Type = type,
        };
    }

    public static Accessory MakeAccessoryItem(
        Guid? worldId = null,
        AccessoryType type = AccessoryType.Ring
    )
    {
        return new Accessory
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
        };
    }

    public static Faction MakeFaction(
        Guid? worldId = null,
        int aggression = 0,
        int reputationSensitivity = 0,
        int riskAversion = 0
    )
    {
        return new Faction
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Faction-{Guid.NewGuid():N}",
            Description = "A test faction",
            Aggression = aggression,
            ReputationSensitivity = reputationSensitivity,
            RiskAversion = riskAversion,
        };
    }

    public static EncounterGroup MakeEncounterGroup(
        Guid worldId,
        Guid locationId,
        Guid factionId
    ) =>
        new()
        {
            WorldId = worldId,
            LocationId = locationId,
            FactionId = factionId,
        };

    public static EncounterGroupMember MakeEncounterGroupMember(
        Guid worldId,
        Guid encounterGroupId,
        Guid creatureId
    ) =>
        new()
        {
            WorldId = worldId,
            EncounterGroupId = encounterGroupId,
            CreatureId = creatureId,
        };

    public static HostileEncounter MakeHostileEncounter(
        Guid worldId,
        Guid playerId,
        Guid locationId,
        Guid encounterGroupId,
        Guid? arrivalOriginLocationId = null,
        EncounterState state = EncounterState.Active
    ) =>
        new()
        {
            WorldId = worldId,
            PlayerId = playerId,
            LocationId = locationId,
            EncounterGroupId = encounterGroupId,
            ArrivalOriginLocationId = arrivalOriginLocationId,
            State = state,
        };

    public static World MakeWorld()
    {
        return new World
        {
            Name = $"World-{Guid.NewGuid():N}",
            Description = "A test world",
            Boundary = new Rectangle(0, 0, 10000, 10000),
        };
    }

    public static Country MakeCountry(Guid worldId, CountryFocus focus = CountryFocus.Scientific)
    {
        return new Country
        {
            WorldId = worldId,
            Name = $"Country-{Guid.NewGuid():N}",
            Description = "A test country",
            Focus = focus,
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

    public static State MakeState(Guid countryId, Guid? worldId = null, Guid? id = null)
    {
        return new State
        {
            Id = id ?? Guid.NewGuid(),
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
        Guid? worldId = null,
        string? name = null,
        Guid? id = null,
        Guid? locationId = null
    )
    {
        return new District
        {
            Id = id ?? Guid.NewGuid(),
            CityId = cityId,
            DistrictType = districtType,
            Name = name ?? $"District-{Guid.NewGuid():N}",
            Description = "A test district",
            WorldId = worldId ?? Guid.NewGuid(),
            LocationId = locationId ?? Guid.NewGuid(),
        };
    }

    public static Room MakeRoom(
        Guid buildingId,
        int capacity = 4,
        Guid? worldId = null,
        Guid? id = null,
        Guid? locationId = null
    )
    {
        return new Room
        {
            Id = id ?? Guid.NewGuid(),
            BuildingId = buildingId,
            Capacity = capacity,
            Name = $"Room-{Guid.NewGuid():N}",
            Description = "A test room",
            FloorNumber = 0,
            WorldId = worldId ?? Guid.NewGuid(),
            LocationId = locationId ?? Guid.NewGuid(),
        };
    }

    public static Location MakeLocation(
        Guid? worldId = null,
        Guid? stateId = null,
        Guid? cityId = null,
        Guid? districtId = null,
        Guid? roomId = null,
        Guid? id = null,
        LocationKind kind = LocationKind.District
    )
    {
        return new Location
        {
            Id = id ?? Guid.NewGuid(),
            WorldId = worldId ?? Guid.NewGuid(),
            StateId = stateId ?? Guid.NewGuid(),
            CityId = cityId,
            DistrictId = districtId,
            RoomId = roomId,
            Kind = kind,
        };
    }

    public static Building MakeBuilding(
        Guid? exteriorLocationId = null,
        Guid? worldId = null,
        string? name = null,
        BuildingType buildingType = BuildingType.House
    )
    {
        return new Building
        {
            ExteriorLocationId = exteriorLocationId ?? Guid.NewGuid(),
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
        Guid? locationId = null,
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
            LocationId = locationId ?? Guid.NewGuid(),
            WorldId = worldId ?? Guid.NewGuid(),
        };
    }

    public static WorldEvent MakeWorldEvent(Guid worldId, Guid? locationId = null)
    {
        return new WorldEvent
        {
            WorldId = worldId,
            Description = "A test world event",
            Date = DateTime.UtcNow,
            Tags = [],
            LocationId = locationId,
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
