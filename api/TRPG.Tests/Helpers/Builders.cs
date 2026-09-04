using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Events;
using TRPG.Application.Combat.Results;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.WorldGeneration.Generators;
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

    public static InGameDate MakeInGameDate(int hour) =>
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
        int lockLevel = 0,
        Guid? worldId = null,
        TimeSpan? unlocksAtPlaytime = null,
        Guid? id = null
    ) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            ConnectorId = connectorId,
            IsLocked = isLocked,
            LockLevel = lockLevel,
            UnlocksAtPlaytime = unlocksAtPlaytime,
            WorldId = worldId ?? Guid.NewGuid(),
        };

    public static TravelConnector MakeTravelConnector(
        Guid connectorId,
        float distance = 1,
        int travelTimeHours = 1,
        float dangerLevel = 0,
        Guid? worldId = null
    ) =>
        new()
        {
            ConnectorId = connectorId,
            Distance = distance,
            TravelTimeHours = travelTimeHours,
            DangerLevel = dangerLevel,
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

    public static SupportAbility MakeHealSupportAbility(
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

    public static SupportAbility MakeBuffSupportAbility(
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
        Guid? previousLocationId = null,
        int birthYear = 1000,
        string name = "Test Creature",
        int level = 1,
        Attributes? baseAttributes = null,
        int? currentHp = null,
        int? currentAp = null,
        int? currentMp = null,
        CreatureState state = default,
        int naturalWeaponMinDamage = 3,
        int naturalWeaponMaxDamage = 3,
        Guid? playerCorpseOwnerId = null,
        Guid? spawnerId = null
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
            PreviousLocationId = previousLocationId,
            Level = level,
            State = state,
            PlayerCorpseOwnerId = playerCorpseOwnerId,
            SpawnerId = spawnerId,
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
            CarryingCapacity = attributes.CarryingCapacity,
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
        Guid? occupantId = null,
        Guid? assignedCreatureId = null,
        Guid? ownerCreatureId = null
    ) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Workstation-{Guid.NewGuid():N}",
            Description = "A test workstation",
            LocationId = locationId ?? Guid.NewGuid(),
            WorkstationType = WorkstationType.Trade,
            OccupantId = occupantId,
            AssignedCreatureId = assignedCreatureId,
            OwnerCreatureId = ownerCreatureId,
        };

    public static Bed MakeBed(
        Guid? worldId = null,
        Guid? locationId = null,
        Guid? occupantId = null,
        Guid? assignedCreatureId = null
    ) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = "Bed",
            Description = "A test bed",
            LocationId = locationId ?? Guid.NewGuid(),
            OccupantId = occupantId,
            AssignedCreatureId = assignedCreatureId,
        };

    public static CreatureSpawner MakeCreatureSpawner(
        Guid worldId,
        Guid locationId,
        IReadOnlyList<CreatureType>? archetypeCreatureTypes = null,
        int maxPopulation = 3,
        int triggerHour = 0,
        DayOfWeek? specificDay = null,
        TimeSpan? lastSyncPlaytime = null
    ) =>
        new()
        {
            WorldId = worldId,
            LocationId = locationId,
            ArchetypeCreatureTypes = (archetypeCreatureTypes ?? [CreatureType.Beast]).ToList(),
            MaxPopulation = maxPopulation,
            TriggerHour = triggerHour,
            SpecificDay = specificDay,
            LastSyncPlaytime = lastSyncPlaytime ?? TimeSpan.Zero,
        };

    public static RestockPolicy MakeRestockPolicy(
        Guid worldId,
        Guid workstationId,
        int triggerHour = 0,
        DayOfWeek? specificDay = null,
        TimeSpan? lastSyncPlaytime = null
    ) =>
        new()
        {
            WorldId = worldId,
            WorkstationId = workstationId,
            TriggerHour = triggerHour,
            SpecificDay = specificDay,
            LastSyncPlaytime = lastSyncPlaytime ?? TimeSpan.Zero,
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
            CarryingCapacity = StatFormulas.CalculateCarryingCapacity(baseAttributes, options),
        };
    }

    public static Item MakeItem(Guid? worldId = null, string? name = null)
    {
        return new Item
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name ?? $"Item-{Guid.NewGuid():N}",
            Description = "A test item",
            Weight = 1,
        };
    }

    public static CrimeWitness MakeCrimeWitness(
        Guid crimeId,
        Guid creatureId,
        Guid? worldId = null,
        CrimeWitnessResolution resolution = CrimeWitnessResolution.Pending
    ) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            CrimeId = crimeId,
            CreatureId = creatureId,
            Resolution = resolution,
        };

    public static Key MakeKey(
        Guid? worldId = null,
        int quantity = 0,
        Guid? ownerId = null,
        OwnerType ownerType = OwnerType.Creature
    )
    {
        var key = new Key
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Key-{Guid.NewGuid():N}",
            Description = "A test key",
            Weight = 1,
            Quantity = quantity,
        };
        if (ownerId != null)
        {
            key.Ownership.OwnerId = ownerId.Value;
            key.Ownership.OwnerType = ownerType;
        }
        return key;
    }

    public static Gold MakeGold(
        Guid? worldId = null,
        int quantity = 0,
        Guid? ownerId = null,
        OwnerType ownerType = OwnerType.Creature
    )
    {
        var gold = new Gold
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = "Gold",
            Quantity = quantity,
        };
        if (ownerId != null)
        {
            gold.Ownership.OwnerId = ownerId.Value;
            gold.Ownership.OwnerType = ownerType;
        }
        return gold;
    }

    public static DoorConnectorKey MakeDoorConnectorKey(
        Guid itemId,
        Guid doorConnectorId,
        Guid? worldId = null
    ) =>
        new()
        {
            ItemId = itemId,
            DoorConnectorId = doorConnectorId,
            WorldId = worldId ?? Guid.NewGuid(),
        };

    public static RoomBooking MakeRoomBooking(
        Guid worldId,
        Guid roomId,
        Guid keyItemId,
        Guid playerId,
        TimeSpan dueAtPlaytime
    ) =>
        new()
        {
            WorldId = worldId,
            RoomId = roomId,
            KeyItemId = keyItemId,
            PlayerId = playerId,
            DueAtPlaytime = dueAtPlaytime,
        };

    public static Weapon MakeWeapon(
        Guid? worldId = null,
        WeaponType type = WeaponType.Sword,
        int minDamage = 5,
        int maxDamage = 15,
        int attacksPerTurn = 1,
        bool isTwoHanded = false,
        int quantity = 0,
        int weight = 8,
        IReadOnlyCollection<ItemModifier>? modifiers = null
    )
    {
        return new Weapon
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = $"Item-{Guid.NewGuid():N}",
            Description = "A test weapon",
            Weight = weight,
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

    public static Armor MakeArmor(
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

    public static Shield MakeShield(
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

    public static Consumable MakeConsumable(
        Guid? worldId = null,
        string? name = null,
        ResourceType resource = ResourceType.Hp,
        int amount = 50,
        int weight = 1
    )
    {
        return new Consumable
        {
            WorldId = worldId ?? Guid.NewGuid(),
            Name = name ?? $"Item-{Guid.NewGuid():N}",
            Description = "A test consumable",
            Weight = weight,
            GoldValue = 10,
            Resource = resource,
            RestoreAmount = amount,
            Duration = 0,
        };
    }

    public static Ammunition MakeAmmunition(Guid? worldId = null, AmmoType type = AmmoType.Arrow)
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

    public static Accessory MakeAccessory(
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

    public static Quest MakeQuest(Guid giverId, Guid? worldId = null, string? name = null)
    {
        return new Quest
        {
            WorldId = worldId ?? Guid.NewGuid(),
            GiverId = giverId,
            Name = name ?? $"Quest-{Guid.NewGuid():N}",
            Description = "A test quest",
            GoldReward = 100,
        };
    }

    public static ExploreLocationObjective MakeExploreLocationObjective(
        Guid questId,
        Guid? worldId = null,
        Guid? locationId = null,
        int requiredAmount = 1,
        string? name = null
    ) =>
        new()
        {
            WorldId = worldId ?? Guid.NewGuid(),
            QuestId = questId,
            Name = name ?? $"Objective-{Guid.NewGuid():N}",
            Description = "A test objective",
            LocationId = locationId,
            RequiredAmount = requiredAmount,
        };

    public static CreatureQuestObjective MakeCreatureQuestObjective(
        Guid creatureId,
        Guid objectiveId,
        Guid? worldId = null,
        int amount = 0
    ) =>
        new()
        {
            CreatureId = creatureId,
            ObjectiveId = objectiveId,
            WorldId = worldId ?? Guid.NewGuid(),
            Amount = amount,
        };

    public static Faction MakeFaction(
        Guid? worldId = null,
        int aggression = 0,
        int reputationSensitivity = 0,
        int riskAversion = 0,
        bool isCityFaction = false,
        CreatureType? creatureType = null
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
            IsCityFaction = isCityFaction,
            CreatureType = creatureType,
        };
    }

    public static FactionMember MakeFactionMember(
        Guid worldId,
        Guid factionId,
        Guid creatureId,
        FactionRole role = FactionRole.Member
    ) =>
        new()
        {
            WorldId = worldId,
            FactionId = factionId,
            CreatureId = creatureId,
            Role = role,
        };

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
        Guid? factionId = null,
        string factionName = "Faction",
        string locationName = "Location",
        IReadOnlyList<HostileEncounterMemberSnapshot>? members = null,
        EncounterState state = EncounterState.Active
    ) =>
        new()
        {
            WorldId = worldId,
            PlayerId = playerId,
            LocationId = locationId,
            FactionId = factionId ?? Guid.NewGuid(),
            FactionName = factionName,
            LocationName = locationName,
            Members = members?.ToList() ?? [],
            State = state,
        };

    public static GuardEncounter MakeGuardEncounter(
        Guid worldId,
        Guid playerId,
        Guid locationId,
        Guid guardCreatureId,
        Guid? cityFactionId = null,
        string guardName = "Guard",
        string locationName = "Location",
        int reputationScore = -50,
        int fineAmount = 50,
        int jailHours = 4,
        EncounterState state = EncounterState.Active
    ) =>
        new()
        {
            WorldId = worldId,
            PlayerId = playerId,
            LocationId = locationId,
            LocationName = locationName,
            GuardCreatureId = guardCreatureId,
            CityFactionId = cityFactionId ?? Guid.NewGuid(),
            GuardName = guardName,
            ReputationScore = reputationScore,
            FineAmount = fineAmount,
            JailHours = jailHours,
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
        Guid? locationId = null,
        string? name = null
    )
    {
        return new Room
        {
            Id = id ?? Guid.NewGuid(),
            BuildingId = buildingId,
            Capacity = capacity,
            Name = name ?? $"Room-{Guid.NewGuid():N}",
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
        LocationKind? kind = null,
        Guid? coarseAnchorLocationId = null
    )
    {
        var locationId = id ?? Guid.NewGuid();

        return new Location
        {
            Id = locationId,
            WorldId = worldId ?? Guid.NewGuid(),
            StateId = stateId ?? Guid.NewGuid(),
            CityId = cityId,
            DistrictId = districtId,
            RoomId = roomId,
            CoarseAnchorLocationId = coarseAnchorLocationId ?? locationId,
            Kind =
                kind
                ?? (
                    roomId != null ? LocationKind.Room
                    : districtId != null ? LocationKind.District
                    : LocationKind.Wilderness
                ),
        };
    }

    public static BuildingOwner MakeBuildingOwner(
        Guid buildingId,
        Guid ownerId,
        Guid? worldId = null
    ) =>
        new()
        {
            BuildingId = buildingId,
            OwnerId = ownerId,
            WorldId = worldId ?? Guid.NewGuid(),
        };

    public static Building MakeBuilding(
        Guid? exteriorLocationId = null,
        Guid? worldId = null,
        string? name = null,
        BuildingType buildingType = BuildingType.House,
        Guid? id = null
    )
    {
        return new Building
        {
            Id = id ?? Guid.NewGuid(),
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

    public static CreatureSkill MakeCreatureSkill(
        Guid creatureId,
        Skill skill = Skill.Melee,
        int level = 1,
        int experience = 0,
        Guid? worldId = null
    ) =>
        new()
        {
            CreatureId = creatureId,
            Skill = skill,
            Level = level,
            Experience = experience,
            WorldId = worldId ?? Guid.NewGuid(),
        };

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

    public static FightEncounter MakeFight(
        Guid worldId,
        Guid playerId,
        IReadOnlyList<Guid> combatantIds
    )
    {
        return new FightEncounter
        {
            WorldId = worldId,
            PlayerId = playerId,
            CombatantIds = combatantIds.ToList(),
        };
    }
}
