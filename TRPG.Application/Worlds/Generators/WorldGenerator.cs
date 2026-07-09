using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Algorithms;
using TRPG.Application.Game;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class WorldGeneratorInput
{
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxBuildingsPerState { get; init; }
    public int MaxCityStates { get; init; }
    public int MaxFactionMembers { get; init; }
    public int MaxHouseholdSize { get; init; }
    public int MaxRuralStates { get; init; }
    public int MinBuildingsPerState { get; init; }
    public int MinCityStates { get; init; }
    public int MinFactionMembers { get; init; }
    public int MinHouseholdSize { get; init; }
    public int MinRuralStates { get; init; }
}

public class WorldGeneratorResult
{
    public required IReadOnlyCollection<CreatureAbility> Abilities { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<InventoryItem> InventoryItems { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<Job> Jobs { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<RoomConnectorKey> RoomConnectorKeys { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyCollection<CreatureSkill> Skills { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required World World { get; init; }
}

public class WorldGenerator(
    BuildingGenerator buildingGenerator,
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    CreatureGenerator creatureGenerator,
    ILogger<WorldGenerator> logger
)
{
    private const double DominantRaceWeight = 0.7;
    private const double FamilyUnitChance = 0.6;
    private const int MinParentBirthYear = 900;
    private const int MaxParentBirthYear = 949;
    private const int YoungestParentingAge = 18;
    private const int AdultAge = 18;
    private const int MaxAdultBirthYear = GameClock.EpochYear - AdultAge;
    private const int MaxShopStaff = 3;

    private static readonly BuildingType[] StandardBuildingTypes =
    [
        BuildingType.ArcaneShop,
        BuildingType.Apothecary,
        BuildingType.Bakery,
        BuildingType.Barracks,
        BuildingType.Blacksmith,
        BuildingType.Carpenter,
        BuildingType.Castle,
        BuildingType.GeneralGoods,
        BuildingType.GuildHall,
        BuildingType.Inn,
        BuildingType.Jail,
        BuildingType.Jeweler,
        BuildingType.Library,
        BuildingType.Stable,
        BuildingType.Tailor,
        BuildingType.Tavern,
        BuildingType.Temple,
    ];

    // Pairwise-disjoint day-off pairs indexed by staff position (0 = owner) so any combination of 2-3 staff
    // drawn from these never leaves a shop fully unstaffed, without needing to coordinate across who's hired when.
    // Used when a shop has 2-3 staffable workstations, where more than one person present at once is fine.
    internal static readonly IReadOnlyList<DayOfWeek>[] StaffDayOffPatterns =
    [
        [DayOfWeek.Saturday, DayOfWeek.Sunday],
        [DayOfWeek.Monday, DayOfWeek.Tuesday],
        [DayOfWeek.Wednesday, DayOfWeek.Thursday],
    ];

    // A genuine partition of the week between exactly 2 people — never both present, since there's only one
    // workstation to share. Position 0 works Sun-Wed (off Thu-Sat); position 1 works Thu-Sat (off Sun-Wed).
    internal static readonly IReadOnlyList<DayOfWeek>[] NonOverlappingDayOffPatterns =
    [
        [DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
        [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday],
    ];

    internal static readonly JobAction[] DayOffActivities =
    [
        JobAction.Idle,
        JobAction.Study,
        JobAction.Pray,
        JobAction.Train,
        JobAction.Sit,
    ];

    private static readonly DayOfWeek[] AllWeekdays = Enum.GetValues<DayOfWeek>();

    internal record ShopEmploymentSlot(
        Guid RoomId,
        Profession EmployeeProfession,
        IReadOnlyList<DayOfWeek> DaysOff,
        HourWindow WorkHours,
        HourWindow? SleepHours = null
    );

    internal record StaffDayOff(
        Guid CreatureId,
        IReadOnlyList<DayOfWeek> DaysOff,
        HourWindow WorkHours
    );

    internal class CityEmploymentContext
    {
        public required List<ShopEmploymentSlot> OpenShopSlots { get; init; }
        public required List<Creature> EligibleForEmployment { get; init; }
        public required List<StaffDayOff> ShopOwnerAssignments { get; init; }
        public required Dictionary<Guid, List<Creature>> HouseholdByMemberId { get; init; }
        public required Dictionary<Guid, Guid> HomeRoomIdByMemberId { get; init; }
        public required HashSet<Guid> FatherIds { get; init; }
        public required List<IdleCandidate> CityIdleCandidates { get; init; }
        public required Guid StateId { get; init; }
        public required Guid WorldId { get; init; }
        public required List<Job> Jobs { get; init; }
    }

    // Everything needed to generate a household (family or single adult), build its house, and register it
    // into the city's shared bookkeeping — reused both for plain residential households and for business
    // owners, who now get a real family instead of living alone above their shop.
    internal class HouseholdGenerationContext
    {
        public required Guid WorldId { get; init; }
        public required City City { get; init; }
        public required Guid StateId { get; init; }
        public required District ResidentialDistrict { get; init; }
        public required CreatureType DominantRace { get; init; }
        public required WorldGeneratorInput GeneratorInput { get; init; }
        public required HashSet<string> UsedBuildingNames { get; init; }
        public required Guid CityFactionId { get; init; }
        public required List<Building> CityBuildings { get; init; }
        public required List<Creature> Creatures { get; init; }
        public required List<FactionMember> FactionMembers { get; init; }
        public required List<Item> Items { get; init; }
        public required List<InventoryItem> InventoryItems { get; init; }
        public required List<CreatureSkill> Skills { get; init; }
        public required List<CreatureAbility> Abilities { get; init; }
        public required List<Job> Jobs { get; init; }
        public required List<BuildingOwner> BuildingOwners { get; init; }
        public required List<Room> Rooms { get; init; }
        public required List<Prop> Props { get; init; }
        public required List<Relationship> Relationships { get; init; }
        public required List<RoomConnectorKey> RoomConnectorKeys { get; init; }
        public required Dictionary<Guid, List<Creature>> HouseholdByMemberId { get; init; }
        public required Dictionary<Guid, Guid> HomeRoomIdByMemberId { get; init; }
        public required HashSet<Guid> FatherIds { get; init; }
        public required List<Creature> EligibleForEmployment { get; init; }
    }

    // A group of creatures who need a sampled day-off destination on one or more shared weekdays. Registered
    // during employment assignment, then resolved all at once per-day so room capacity is respected correctly
    // (capacity is inherently a same-day concept — a day-off group consumes it, not each individual separately).
    private record DayOffNeed(
        IReadOnlyList<Guid> ParticipantIds,
        Guid HomeRoomId,
        JobAction Action,
        HourWindow Hours,
        bool ExcludeTavern,
        bool IsUnemployedActivity
    );

    public async Task<WorldGeneratorResult> Generate(
        WorldGeneratorInput generatorInput,
        CancellationToken cancellationToken
    )
    {
        if (generatorInput.HousesPerCity > BuildingGenerator.Names[BuildingType.House].Length)
        {
            throw new InvalidOperationException(
                $"HousesPerCity ({generatorInput.HousesPerCity}) cannot exceed the house name pool size ({BuildingGenerator.Names[BuildingType.House].Length})."
            );
        }

        var sw = Stopwatch.StartNew();
        var worldId = Guid.NewGuid();

        var groundedDescription = $"""
            {generatorInput.Description} This remains a low-fantasy world where knights,
            mercenaries, blacksmiths, and mages are established, respected roles, and swords
            and plate armor are still standard equipment. Any technological or aesthetic
            theme should layer on top of this as mood and atmosphere — not replace or
            obsolete these roles and equipment. The peoples of this world are Humans, Elves,
            Dwarves, Orcs, Halflings, and Gnomes — do not invent other playable races (no
            Tieflings, Dragonborn, Elementals, or similar) though monstrous threats like
            undead, demons, and beasts may lurk at the margins of civilization.
            """;

        var factions = (
            await factionsGenerator.Generate(
                new FactionsGeneratorInput
                {
                    WorldId = worldId,
                    Description = groundedDescription,
                    Count = generatorInput.FactionCount,
                },
                cancellationToken
            )
        ).ToList();
        var namedFactionCount = factions.Count;

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput
            {
                WorldId = worldId,
                Description = groundedDescription,
                MaxRuralStates = generatorInput.MaxRuralStates,
                MaxCityStates = generatorInput.MaxCityStates,
                MinRuralStates = generatorInput.MinRuralStates,
                MinCityStates = generatorInput.MinCityStates,
            },
            cancellationToken
        );

        var buildings = new List<Building>();
        var creatures = new List<Creature>();
        var buildingOwners = new List<BuildingOwner>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var rooms = new List<Room>();
        var props = new List<Prop>();
        var skills = new List<CreatureSkill>();
        var abilities = new List<CreatureAbility>();
        var jobs = new List<Job>();
        var roomConnectorKeys = new List<RoomConnectorKey>();
        var relationships = new List<Relationship>();
        var guildHallIndex = 0;

        var stateById = geography.States.ToDictionary(s => s.Id);
        var districtsByCityId = geography
            .Districts.GroupBy(d => d.CityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var city in geography.Cities)
        {
            var state = stateById[city.StateId];
            var dominantRace = geography.DominantRaceByCountryId[city.CountryId];
            var usedBuildingNames = new HashSet<string>();
            var cityDistricts = districtsByCityId[city.Id].ToDictionary(d => d.DistrictType);

            var cityFaction = new Faction
            {
                WorldId = worldId,
                Name = $"The People of {city.Name}",
                Description = $"The common folk of {city.Name}.",
                IsCityFaction = true,
            };
            factions.Add(cityFaction);

            var cityBuildings = new List<Building>();
            var cityIdleCandidates = cityDistricts
                .Values.Select(d => new IdleCandidate(
                    null,
                    d.Id,
                    int.MaxValue,
                    DistrictGenerator.Popularity[d.DistrictType],
                    null
                ))
                .ToList();
            var openShopSlots = new List<ShopEmploymentSlot>();
            var shopOwnerAssignments = new List<StaffDayOff>();
            var eligibleForEmployment = new List<Creature>();
            var householdByMemberId = new Dictionary<Guid, List<Creature>>();
            var homeRoomIdByMemberId = new Dictionary<Guid, Guid>();
            var fatherIds = new HashSet<Guid>();
            var residentialDistrict = cityDistricts[DistrictType.Residential];

            var householdContext = new HouseholdGenerationContext
            {
                WorldId = worldId,
                City = city,
                StateId = state.Id,
                ResidentialDistrict = residentialDistrict,
                DominantRace = dominantRace,
                GeneratorInput = generatorInput,
                UsedBuildingNames = usedBuildingNames,
                CityFactionId = cityFaction.Id,
                CityBuildings = cityBuildings,
                Creatures = creatures,
                FactionMembers = factionMembers,
                Items = items,
                InventoryItems = inventoryItems,
                Skills = skills,
                Abilities = abilities,
                Jobs = jobs,
                BuildingOwners = buildingOwners,
                Rooms = rooms,
                Props = props,
                Relationships = relationships,
                RoomConnectorKeys = roomConnectorKeys,
                HouseholdByMemberId = householdByMemberId,
                HomeRoomIdByMemberId = homeRoomIdByMemberId,
                FatherIds = fatherIds,
                EligibleForEmployment = eligibleForEmployment,
            };

            foreach (var type in StandardBuildingTypes)
            {
                if (
                    !cityDistricts.TryGetValue(
                        DistrictGenerator.DistrictTypeByBuildingType[type],
                        out var district
                    )
                )
                {
                    continue;
                }

                var owner = GenerateAndRegisterHousehold(
                    householdContext,
                    GetProfessionForBuilding(type)
                )!;

                var memberIds = new List<Guid> { owner.Id };
                var memberCreatures = new List<CreatureGeneratorResult>();

                if (type == BuildingType.GuildHall)
                {
                    var numMembers = Random.Shared.Next(
                        generatorInput.MinFactionMembers,
                        generatorInput.MaxFactionMembers + 1
                    );
                    for (var m = 1; m < numMembers; m++)
                    {
                        var memberRace = PickCreatureType(dominantRace);
                        var memberCreature = creatureGenerator.Generate(
                            new CreatureGeneratorInput(
                                memberRace,
                                Profession.Mercenary,
                                worldId,
                                state.Id,
                                state.Id
                            )
                            {
                                MaxBirthYear = MaxAdultBirthYear,
                            }
                        );
                        memberCreature.Creature.CityId = city.Id;
                        memberCreature.Creature.DistrictId = district.Id;
                        memberCreatures.Add(memberCreature);
                        memberIds.Add(memberCreature.Creature.Id);
                    }
                }

                var isLockable = type is not (BuildingType.Inn or BuildingType.Tavern);
                var buildingName = SettlementNameGenerator.GenerateBuildingName(
                    dominantRace,
                    type,
                    usedBuildingNames
                );

                var buildingResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(
                        state.Id,
                        city.Id,
                        district.Id,
                        owner.Id,
                        type,
                        worldId
                    )
                    {
                        Name = buildingName,
                        MemberIds = memberIds,
                        IsLockable = isLockable,
                    }
                );

                if (isLockable)
                {
                    var frontDoor = buildingResult
                        .Props.OfType<RoomConnector>()
                        .First(c => c.DestinationRoomId == null);
                    foreach (var residentId in memberIds)
                    {
                        var keyItem = new Item
                        {
                            WorldId = worldId,
                            Name = $"Key to {buildingResult.Building.Name}",
                            Description = $"A key that unlocks {buildingResult.Building.Name}.",
                        };
                        items.Add(keyItem);
                        inventoryItems.Add(
                            new InventoryItem
                            {
                                CreatureId = residentId,
                                ItemId = keyItem.Id,
                                Quantity = 1,
                                WorldId = worldId,
                            }
                        );
                        roomConnectorKeys.Add(
                            new RoomConnectorKey
                            {
                                ItemId = keyItem.Id,
                                RoomConnectorId = frontDoor.Id,
                                WorldId = worldId,
                            }
                        );
                    }
                }

                var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);
                var groundFloorRoomId = groundFloorRoom.Id;
                if (
                    type
                    is not (
                        BuildingType.Jail
                        or BuildingType.Castle
                        or BuildingType.Barracks
                        or BuildingType.GuildHall
                    )
                )
                {
                    cityIdleCandidates.Add(
                        new IdleCandidate(
                            groundFloorRoomId,
                            district.Id,
                            groundFloorRoom.Capacity,
                            BuildingGenerator.Popularity[type],
                            type
                        )
                    );
                }

                if (type == BuildingType.GuildHall)
                {
                    var guildFactionId =
                        namedFactionCount > 0
                            ? factions[guildHallIndex++ % namedFactionCount].Id
                            : (Guid?)null;

                    if (guildFactionId != null)
                    {
                        buildingResult.Building.FactionId = guildFactionId;
                        factionMembers.Add(
                            new FactionMember
                            {
                                FactionId = guildFactionId.Value,
                                CreatureId = owner.Id,
                                Role = FactionRole.Leader,
                                WorldId = worldId,
                            }
                        );
                    }

                    foreach (var memberCreature in memberCreatures)
                    {
                        if (guildFactionId != null)
                        {
                            factionMembers.Add(
                                new FactionMember
                                {
                                    FactionId = guildFactionId.Value,
                                    CreatureId = memberCreature.Creature.Id,
                                    Role = FactionRole.Member,
                                    WorldId = worldId,
                                }
                            );
                        }

                        factionMembers.Add(
                            new FactionMember
                            {
                                FactionId = cityFaction.Id,
                                CreatureId = memberCreature.Creature.Id,
                                Role = FactionRole.Member,
                                WorldId = worldId,
                            }
                        );
                        memberCreature.Creature.RoomId = groundFloorRoomId;
                        creatures.Add(memberCreature.Creature);
                        items.AddRange(memberCreature.Items);
                        inventoryItems.AddRange(memberCreature.InventoryItems);
                        skills.AddRange(memberCreature.Skills);
                        abilities.AddRange(memberCreature.Abilities);

                        var memberBedRoomId = buildingResult
                            .Props.OfType<Bed>()
                            .First(b => b.AssignedCreatureId == memberCreature.Creature.Id)
                            .RoomId;
                        jobs.AddRange(
                            JobGenerator.Generate(
                                state.Id,
                                memberCreature.Creature.Id,
                                memberBedRoomId,
                                null,
                                groundFloorRoomId,
                                worldId
                            )
                        );
                    }
                }

                // The owner already has Sleep/Idle jobs anchored at their family home from household
                // registration above — only their Work job (and any shift-specific overrides) is added here.
                if (type == BuildingType.Inn)
                {
                    GenerateInnStaffing(
                        state.Id,
                        worldId,
                        owner.Id,
                        groundFloorRoomId,
                        jobs,
                        shopOwnerAssignments,
                        openShopSlots
                    );
                }
                else if (type == BuildingType.GuildHall)
                {
                    jobs.Add(
                        JobGenerator.GenerateWork(state.Id, owner.Id, groundFloorRoomId, worldId)
                    );
                }
                else
                {
                    var workHours = GetWorkHoursForBuilding(type);
                    var sleepHours = GetSleepHoursForBuilding(type);
                    jobs.Add(
                        JobGenerator.GenerateWork(
                            state.Id,
                            owner.Id,
                            groundFloorRoomId,
                            worldId,
                            workHours
                        )
                    );
                    ApplySleepOverride(owner.Id, sleepHours, state.Id, worldId, jobs);

                    var staffableWorkstationCount = Math.Max(
                        1,
                        buildingResult
                            .Props.OfType<Workstation>()
                            .Count(w => w.WorkstationType != WorkstationType.Reading)
                    );

                    if (staffableWorkstationCount == 1)
                    {
                        shopOwnerAssignments.Add(
                            new StaffDayOff(owner.Id, NonOverlappingDayOffPatterns[0], workHours)
                        );
                        openShopSlots.Add(
                            new ShopEmploymentSlot(
                                groundFloorRoomId,
                                GetEmployeeProfessionForBuilding(type),
                                NonOverlappingDayOffPatterns[1],
                                workHours,
                                sleepHours
                            )
                        );
                    }
                    else
                    {
                        var totalStaff = Math.Min(staffableWorkstationCount, MaxShopStaff);
                        shopOwnerAssignments.Add(
                            new StaffDayOff(owner.Id, StaffDayOffPatterns[0], workHours)
                        );
                        for (var position = 1; position < totalStaff; position++)
                        {
                            openShopSlots.Add(
                                new ShopEmploymentSlot(
                                    groundFloorRoomId,
                                    GetEmployeeProfessionForBuilding(type),
                                    StaffDayOffPatterns[position],
                                    workHours,
                                    sleepHours
                                )
                            );
                        }
                    }
                }

                cityBuildings.Add(buildingResult.Building);
                buildingOwners.Add(
                    new BuildingOwner
                    {
                        BuildingId = buildingResult.Building.Id,
                        OwnerId = owner.Id,
                        WorldId = worldId,
                    }
                );
                rooms.AddRange(buildingResult.Rooms);
                props.AddRange(buildingResult.Props);
            }

            for (var h = 0; h < generatorInput.HousesPerCity; h++)
            {
                GenerateAndRegisterHousehold(householdContext, null);
            }

            AssignEmployment(
                new CityEmploymentContext
                {
                    OpenShopSlots = openShopSlots,
                    EligibleForEmployment = eligibleForEmployment,
                    ShopOwnerAssignments = shopOwnerAssignments,
                    HouseholdByMemberId = householdByMemberId,
                    HomeRoomIdByMemberId = homeRoomIdByMemberId,
                    FatherIds = fatherIds,
                    CityIdleCandidates = cityIdleCandidates,
                    StateId = state.Id,
                    WorldId = worldId,
                    Jobs = jobs,
                }
            );

            buildings.AddRange(cityBuildings);
        }

        foreach (var state in geography.States)
        {
            var count = Random.Shared.Next(
                generatorInput.MinBuildingsPerState,
                generatorInput.MaxBuildingsPerState + 1
            );
            var usedNames = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var result = DungeonGenerator.Generate(
                    new DungeonGeneratorInput(state.Id, usedNames, worldId)
                );
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.Add(result.Room);
            }
        }

        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(
                creatures,
                stateById,
                factionMembers,
                factions,
                relationships,
                jobs,
                rooms,
                buildings,
                buildingOwners
            )
        );

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult
        {
            World = geography.World,
            Countries = geography.Countries,
            States = geography.States,
            Cities = geography.Cities,
            Districts = geography.Districts,
            Roads = geography.Roads,
            Factions = factions,
            Buildings = buildings,
            Creatures = creatures,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            InventoryItems = inventoryItems,
            Rooms = rooms,
            Props = props,
            Skills = skills,
            Abilities = abilities,
            Jobs = jobs,
            RoomConnectorKeys = roomConnectorKeys,
            Relationships = relationships,
        };
    }

    // Generates a household (family or single adult), builds its house, and registers everyone into the city's
    // shared bookkeeping — Sleep/Idle jobs anchored at home, relationships, keys, employment eligibility.
    // When designatedProfession is set, the household's lead adult (the father in a family — the mother may
    // already be committed to Homemaker duty — or the sole adult otherwise) takes that profession immediately
    // instead of joining the Unemployed pool, and is returned so the caller can use them as a building owner.
    // This is how business owners (shop owners, guild leaders, etc.) end up with a real family of their own
    // instead of living alone above their business.
    internal Creature? GenerateAndRegisterHousehold(
        HouseholdGenerationContext context,
        Profession? designatedProfession
    )
    {
        var householdResult =
            Random.Shared.NextDouble() < FamilyUnitChance
                ? GenerateFamilyHousehold(
                    context.DominantRace,
                    context.WorldId,
                    context.StateId,
                    context.GeneratorInput
                )
                : GenerateSingleHousehold(context.DominantRace, context.WorldId, context.StateId);
        var household = householdResult.Members;
        context.Relationships.AddRange(householdResult.Relationships);

        var householdCreatures = household.Select(m => m.Creature).ToList();
        foreach (var member in household)
        {
            member.Creature.CityId = context.City.Id;
            context.HouseholdByMemberId[member.Creature.Id] = householdCreatures;
        }

        if (household.Count >= 2)
        {
            context.FatherIds.Add(household[1].Creature.Id);
        }

        var designatedWorker =
            designatedProfession != null
                ? household.Count >= 2
                    ? household[1]
                    : household[0]
                : null;
        if (designatedWorker != null)
        {
            designatedWorker.Creature.Profession = designatedProfession;
        }

        var houseOwner = household[0];
        var houseName = SettlementNameGenerator.GenerateBuildingName(
            context.DominantRace,
            BuildingType.House,
            context.UsedBuildingNames
        );
        var houseResult = buildingGenerator.Generate(
            new BuildingGeneratorInput(
                context.StateId,
                context.City.Id,
                context.ResidentialDistrict.Id,
                houseOwner.Creature.Id,
                BuildingType.House,
                context.WorldId
            )
            {
                Name = houseName,
                MemberIds = household.Select(m => m.Creature.Id).ToList(),
                BedroomGroups = householdResult.BedroomGroups,
                IsLockable = true,
            }
        );

        var houseFrontDoor = houseResult
            .Props.OfType<RoomConnector>()
            .First(c => c.DestinationRoomId == null);
        foreach (var resident in household)
        {
            var houseKeyItem = new Item
            {
                WorldId = context.WorldId,
                Name = $"Key to {houseResult.Building.Name}",
                Description = $"A key that unlocks {houseResult.Building.Name}.",
            };
            context.Items.Add(houseKeyItem);
            context.InventoryItems.Add(
                new InventoryItem
                {
                    CreatureId = resident.Creature.Id,
                    ItemId = houseKeyItem.Id,
                    Quantity = 1,
                    WorldId = context.WorldId,
                }
            );
            context.RoomConnectorKeys.Add(
                new RoomConnectorKey
                {
                    ItemId = houseKeyItem.Id,
                    RoomConnectorId = houseFrontDoor.Id,
                    WorldId = context.WorldId,
                }
            );
        }

        var homeRoom = houseResult.Rooms.First(r => r.FloorNumber == 0);
        var homeRoomId = homeRoom.Id;

        foreach (var member in household)
        {
            context.Creatures.Add(member.Creature);
            context.FactionMembers.Add(
                new FactionMember
                {
                    FactionId = context.CityFactionId,
                    CreatureId = member.Creature.Id,
                    Role = FactionRole.Member,
                    WorldId = context.WorldId,
                }
            );
            context.Items.AddRange(member.Items);
            context.InventoryItems.AddRange(member.InventoryItems);
            context.Skills.AddRange(member.Skills);
            context.Abilities.AddRange(member.Abilities);

            var memberBedRoomId = houseResult
                .Props.OfType<Bed>()
                .First(b => b.AssignedCreatureId == member.Creature.Id)
                .RoomId;
            context.Jobs.Add(
                JobGenerator.GenerateSleep(
                    context.StateId,
                    member.Creature.Id,
                    memberBedRoomId,
                    context.WorldId
                )
            );
            // Everyone's baseline is home — a Work/DayOff job overrides this during work hours for whoever
            // ends up employed, so this doubles as their after-shift routine for free.
            context.Jobs.Add(
                JobGenerator.GenerateIdle(
                    context.StateId,
                    member.Creature.Id,
                    homeRoomId,
                    context.WorldId
                )
            );
            member.Creature.RoomId = homeRoomId;
            member.Creature.DistrictId = context.ResidentialDistrict.Id;

            context.HomeRoomIdByMemberId[member.Creature.Id] = homeRoomId;
            if (member.Creature.Profession == Profession.Unemployed)
            {
                context.EligibleForEmployment.Add(member.Creature);
            }
        }

        context.CityBuildings.Add(houseResult.Building);
        context.BuildingOwners.Add(
            new BuildingOwner
            {
                BuildingId = houseResult.Building.Id,
                OwnerId = houseOwner.Creature.Id,
                WorldId = context.WorldId,
            }
        );
        context.Rooms.AddRange(houseResult.Rooms);
        context.Props.AddRange(houseResult.Props);

        return designatedWorker?.Creature;
    }

    internal HouseholdResult GenerateFamilyHousehold(
        CreatureType dominantRace,
        Guid worldId,
        Guid stateId,
        WorldGeneratorInput generatorInput
    )
    {
        var creatureType = PickCreatureType(dominantRace);
        var lastName = CreatureGenerator.GetLastName(creatureType);

        var motherFirstName = CreatureGenerator.GetFirstName(creatureType, Gender.Female);
        var mother = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                creatureType,
                Profession.Unemployed,
                worldId,
                stateId,
                stateId
            )
            {
                Gender = Gender.Female,
                Name = CreatureGenerator.ComposeFullName(
                    creatureType,
                    Gender.Female,
                    motherFirstName,
                    lastName
                ),
                MinBirthYear = MinParentBirthYear,
                MaxBirthYear = MaxParentBirthYear,
            }
        );
        var fatherFirstName = CreatureGenerator.GetFirstName(creatureType, Gender.Male);
        var father = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                creatureType,
                Profession.Unemployed,
                worldId,
                stateId,
                stateId
            )
            {
                Gender = Gender.Male,
                Name = CreatureGenerator.ComposeFullName(
                    creatureType,
                    Gender.Male,
                    fatherFirstName,
                    lastName
                ),
                MinBirthYear = MinParentBirthYear,
                MaxBirthYear = MaxParentBirthYear,
            }
        );

        var householdSize = Random.Shared.Next(
            generatorInput.MinHouseholdSize,
            generatorInput.MaxHouseholdSize + 1
        );
        var kidCount = Math.Max(0, householdSize - 2);
        var oldestParentBirthYear = Math.Max(mother.Creature.BirthYear, father.Creature.BirthYear);
        var minKidBirthYear = oldestParentBirthYear + YoungestParentingAge;

        var kids = new List<CreatureGeneratorResult>();
        for (var k = 0; k < kidCount; k++)
        {
            var kidGender = Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female;
            var kidFirstName = CreatureGenerator.GetFirstName(creatureType, kidGender);
            var kid = creatureGenerator.Generate(
                new CreatureGeneratorInput(
                    creatureType,
                    Profession.Unemployed,
                    worldId,
                    stateId,
                    stateId
                )
                {
                    Gender = kidGender,
                    Name = CreatureGenerator.ComposeFullName(
                        creatureType,
                        kidGender,
                        kidFirstName,
                        lastName
                    ),
                    MinBirthYear = minKidBirthYear,
                    MaxBirthYear = GameClock.EpochYear - 1,
                }
            );
            kid.Creature.Profession =
                GameClock.EpochYear - kid.Creature.BirthYear < AdultAge
                    ? null
                    : Profession.Unemployed;
            kids.Add(kid);
        }

        if (kidCount > 0)
        {
            mother.Creature.Profession = Profession.Homemaker;
        }

        var relationships = BuildFamilyRelationships(worldId, mother, father, kids);

        var members = new List<CreatureGeneratorResult> { mother, father };
        members.AddRange(kids);

        List<IReadOnlyList<Guid>> bedroomGroups =
        [
            [mother.Creature.Id, father.Creature.Id],
        ];
        bedroomGroups.AddRange(kids.Select(kid => (IReadOnlyList<Guid>)[kid.Creature.Id]));

        return new HouseholdResult(members, bedroomGroups, relationships);
    }

    private static IReadOnlyList<Relationship> BuildFamilyRelationships(
        Guid worldId,
        CreatureGeneratorResult mother,
        CreatureGeneratorResult father,
        IReadOnlyList<CreatureGeneratorResult> kids
    )
    {
        var relationships = new List<Relationship>();

        relationships.Add(
            new Relationship
            {
                SubjectId = mother.Creature.Id,
                RelativeId = father.Creature.Id,
                RelationshipType = RelationshipType.Husband,
                WorldId = worldId,
            }
        );
        relationships.Add(
            new Relationship
            {
                SubjectId = father.Creature.Id,
                RelativeId = mother.Creature.Id,
                RelationshipType = RelationshipType.Wife,
                WorldId = worldId,
            }
        );

        foreach (var kid in kids)
        {
            var kidRoleForParent =
                kid.Creature.Gender == Gender.Male
                    ? RelationshipType.Son
                    : RelationshipType.Daughter;
            relationships.Add(
                new Relationship
                {
                    SubjectId = kid.Creature.Id,
                    RelativeId = mother.Creature.Id,
                    RelationshipType = RelationshipType.Mother,
                    WorldId = worldId,
                }
            );
            relationships.Add(
                new Relationship
                {
                    SubjectId = kid.Creature.Id,
                    RelativeId = father.Creature.Id,
                    RelationshipType = RelationshipType.Father,
                    WorldId = worldId,
                }
            );
            relationships.Add(
                new Relationship
                {
                    SubjectId = mother.Creature.Id,
                    RelativeId = kid.Creature.Id,
                    RelationshipType = kidRoleForParent,
                    WorldId = worldId,
                }
            );
            relationships.Add(
                new Relationship
                {
                    SubjectId = father.Creature.Id,
                    RelativeId = kid.Creature.Id,
                    RelationshipType = kidRoleForParent,
                    WorldId = worldId,
                }
            );
        }

        foreach (var kid in kids)
        {
            foreach (var sibling in kids)
            {
                if (kid == sibling)
                {
                    continue;
                }

                relationships.Add(
                    new Relationship
                    {
                        SubjectId = kid.Creature.Id,
                        RelativeId = sibling.Creature.Id,
                        RelationshipType =
                            sibling.Creature.Gender == Gender.Male
                                ? RelationshipType.Brother
                                : RelationshipType.Sister,
                        WorldId = worldId,
                    }
                );
            }
        }

        return relationships;
    }

    // A lone householder must be an adult — there's no parent around to have raised a kid living alone.
    internal HouseholdResult GenerateSingleHousehold(
        CreatureType dominantRace,
        Guid worldId,
        Guid stateId
    )
    {
        var creatureType = PickCreatureType(dominantRace);
        var member = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                creatureType,
                Profession.Unemployed,
                worldId,
                stateId,
                stateId
            )
            {
                MaxBirthYear = MaxAdultBirthYear,
            }
        );

        return new HouseholdResult(
            [member],
            [
                [member.Creature.Id],
            ],
            []
        );
    }

    private static CreatureType PickCreatureType(CreatureType dominantRace)
    {
        if (Random.Shared.NextDouble() < DominantRaceWeight)
        {
            return dominantRace;
        }

        var others = CreatureTypes.Humanoid.Where(r => r != dominantRace).ToArray();
        return others[Random.Shared.Next(others.Length)];
    }

    private static Profession GetProfessionForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Tavern => Profession.Bartender,
            BuildingType.Blacksmith => Profession.Blacksmith,
            BuildingType.Temple => Profession.Cleric,
            BuildingType.Library => Profession.Scholar,
            BuildingType.GeneralGoods => Profession.Merchant,
            BuildingType.Apothecary => Profession.Alchemist,
            BuildingType.Bakery => Profession.Baker,
            BuildingType.Stable => Profession.StableMaster,
            BuildingType.ArcaneShop => Profession.Mage,
            BuildingType.GuildHall => Profession.Politician,
            BuildingType.Castle => Profession.Politician,
            BuildingType.Jail => Profession.Guard,
            BuildingType.Inn => Profession.Innkeeper,
            BuildingType.Barracks => Profession.Guard,
            BuildingType.Tailor => Profession.Tailor,
            BuildingType.Carpenter => Profession.Carpenter,
            BuildingType.Jeweler => Profession.Jeweler,
            _ => throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "No profession mapped for this building type."
            ),
        };
    }

    // Castle employees are the knights guarding the ruler, distinct from the Politician who owns it — every
    // other shop's employees do the same trade as its owner.
    private static Profession GetEmployeeProfessionForBuilding(BuildingType type)
    {
        return type == BuildingType.Castle ? Profession.Knight : GetProfessionForBuilding(type);
    }

    // Inn runs its own day/night shift model entirely separately from this — it never calls this.
    private static HourWindow GetWorkHoursForBuilding(BuildingType type)
    {
        return type switch
        {
            BuildingType.Bakery => new HourWindow(6, 14),
            BuildingType.Tavern => new HourWindow(16, 4),
            BuildingType.Blacksmith or BuildingType.Carpenter or BuildingType.Stable =>
                new HourWindow(7, 17),
            BuildingType.ArcaneShop or BuildingType.Library => new HourWindow(9, 19),
            BuildingType.Castle or BuildingType.Jail or BuildingType.Barracks => new HourWindow(
                8,
                20
            ),
            _ => new HourWindow(8, 18),
        };
    }

    // Only Tavern's late close needs a Sleep window shifted off the universal 22-6 — every other building's
    // Work hours already comfortably fit inside it. Inn's night shift is handled entirely separately.
    private static HourWindow? GetSleepHoursForBuilding(BuildingType type)
    {
        return type == BuildingType.Tavern ? new HourWindow(4, 12) : null;
    }

    // A front desk that's never unstaffed needs two non-interchangeable roles rather than the usual staffing
    // model — a day shift and a night shift, each independently covered by a non-overlapping pair (the owner
    // fills one of the day-shift slots), for 4 total staff. Night-shift staff sleep during the day instead.
    // The owner's Sleep/Idle jobs are already anchored at their family home from household registration —
    // only their day-shift Work job (and the 3 additional night/day-shift slots) is added here.
    private static void GenerateInnStaffing(
        Guid stateId,
        Guid worldId,
        Guid ownerId,
        Guid groundFloorRoomId,
        List<Job> jobs,
        List<StaffDayOff> shopOwnerAssignments,
        List<ShopEmploymentSlot> openShopSlots
    )
    {
        var dayShiftHours = new HourWindow(6, 18);
        var nightShiftHours = new HourWindow(18, 6);
        var nightShiftSleepHours = new HourWindow(6, 14);

        jobs.Add(
            JobGenerator.GenerateWork(stateId, ownerId, groundFloorRoomId, worldId, dayShiftHours)
        );

        shopOwnerAssignments.Add(
            new StaffDayOff(ownerId, NonOverlappingDayOffPatterns[0], dayShiftHours)
        );

        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[1],
                dayShiftHours
            )
        );
        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[0],
                nightShiftHours,
                nightShiftSleepHours
            )
        );
        openShopSlots.Add(
            new ShopEmploymentSlot(
                groundFloorRoomId,
                Profession.Innkeeper,
                NonOverlappingDayOffPatterns[1],
                nightShiftHours,
                nightShiftSleepHours
            )
        );
    }

    // Fills open shop slots from the pool of employment-eligible household adults; leftovers become Unemployed.
    // Shop owners always have a job, so they only need their day-off schedule assigned here.
    internal static void AssignEmployment(CityEmploymentContext context)
    {
        var needsByDay = AllWeekdays.ToDictionary(day => day, _ => new List<DayOffNeed>());

        var slotIndex = 0;
        foreach (var adult in context.EligibleForEmployment)
        {
            if (slotIndex >= context.OpenShopSlots.Count)
            {
                adult.Profession = Profession.Unemployed;
                RegisterUnemployedWeek(adult, needsByDay, context);
                continue;
            }

            var slot = context.OpenShopSlots[slotIndex++];
            adult.Profession = slot.EmployeeProfession;
            context.Jobs.Add(
                JobGenerator.GenerateWork(
                    context.StateId,
                    adult.Id,
                    slot.RoomId,
                    context.WorldId,
                    slot.WorkHours
                )
            );
            ApplySleepOverride(
                adult.Id,
                slot.SleepHours,
                context.StateId,
                context.WorldId,
                context.Jobs
            );
            RegisterDayOff(adult.Id, slot.DaysOff, slot.WorkHours, needsByDay, context);
        }

        foreach (var ownerAssignment in context.ShopOwnerAssignments)
        {
            RegisterDayOff(
                ownerAssignment.CreatureId,
                ownerAssignment.DaysOff,
                ownerAssignment.WorkHours,
                needsByDay,
                context
            );
        }

        GenerateDayOffJobs(needsByDay, context);
    }

    // A creature's Sleep job was already generated with the default window back when they were still just
    // a generic household member — Tavern/Inn-night-shift hires (and owners) need it replaced with a shifted
    // window instead.
    private static void ApplySleepOverride(
        Guid creatureId,
        HourWindow? sleepHours,
        Guid stateId,
        Guid worldId,
        List<Job> jobs
    )
    {
        if (sleepHours == null)
        {
            return;
        }

        var existingSleep = jobs.First(j =>
            j.CreatureId == creatureId && j.Action == JobAction.Sleep
        );
        jobs.Remove(existingSleep);
        jobs.Add(
            JobGenerator.GenerateSleep(
                stateId,
                creatureId,
                existingSleep.RoomId!.Value,
                worldId,
                sleepHours
            )
        );
    }

    // A household's father gets a shared family activity on his days off instead of a solo one, as long as his
    // wife stayed home to raise their kids — everyone else (single/childless adults, shop owners) gets a solo one.
    private static void RegisterDayOff(
        Guid adultId,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var household = context.HouseholdByMemberId[adultId];
        var homemaker = context.FatherIds.Contains(adultId)
            ? household.FirstOrDefault(m => m.Profession == Profession.Homemaker)
            : null;

        if (homemaker != null)
        {
            var father = household.First(m => m.Id == adultId);
            var kids = household.Where(m => m.Id != adultId && m.Id != homemaker.Id).ToList();
            RegisterFamilyDay(father, homemaker, kids, daysOff, workHours, needsByDay, context);
            return;
        }

        RegisterSoloDayOff(
            adultId,
            context.HomeRoomIdByMemberId[adultId],
            daysOff,
            workHours,
            needsByDay
        );
    }

    // Excludes Tavern specifically when a genuine minor (a child too young to be employment-eligible) is
    // in the group — everyone else's day off can land there like any other public building.
    private static void RegisterFamilyDay(
        Creature father,
        Creature homemaker,
        IReadOnlyList<Creature> kids,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
        var participantIds = new List<Guid> { father.Id, homemaker.Id };
        participantIds.AddRange(kids.Select(k => k.Id));
        var excludeTavern = kids.Any(k => k.Profession == null);
        var homeRoomId = context.HomeRoomIdByMemberId[father.Id];
        var need = new DayOffNeed(
            participantIds,
            homeRoomId,
            action,
            workHours,
            excludeTavern,
            false
        );

        foreach (var day in daysOff)
        {
            needsByDay[day].Add(need);
        }
    }

    private static void RegisterSoloDayOff(
        Guid creatureId,
        Guid homeRoomId,
        IReadOnlyList<DayOfWeek> daysOff,
        HourWindow workHours,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay
    )
    {
        var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
        var need = new DayOffNeed([creatureId], homeRoomId, action, workHours, false, false);

        foreach (var day in daysOff)
        {
            needsByDay[day].Add(need);
        }
    }

    // Unemployed adults have no baseline schedule at all — every weekday independently rolls its own activity.
    private static void RegisterUnemployedWeek(
        Creature adult,
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        var homeRoomId = context.HomeRoomIdByMemberId[adult.Id];

        foreach (var day in AllWeekdays)
        {
            var action = DayOffActivities[Random.Shared.Next(DayOffActivities.Length)];
            needsByDay[day]
                .Add(
                    new DayOffNeed(
                        [adult.Id],
                        homeRoomId,
                        action,
                        new HourWindow(6, 22),
                        false,
                        true
                    )
                );
        }
    }

    // Resolved one day at a time — capacity is inherently a same-day concept, so each weekday gets its own fresh
    // capacity-tracking pass rather than sharing one pool across the whole week.
    private static void GenerateDayOffJobs(
        Dictionary<DayOfWeek, List<DayOffNeed>> needsByDay,
        CityEmploymentContext context
    )
    {
        foreach (var (day, needs) in needsByDay)
        {
            var remainingCapacity = context.CityIdleCandidates.Select(c => c.Capacity).ToList();

            foreach (var need in needs)
            {
                var roomId = PickDayOffRoom(need, context.CityIdleCandidates, remainingCapacity);

                foreach (var participantId in need.ParticipantIds)
                {
                    context.Jobs.Add(
                        need.IsUnemployedActivity
                            ? JobGenerator.GenerateUnemployedDayActivity(
                                context.StateId,
                                participantId,
                                need.Action,
                                roomId,
                                day,
                                context.WorldId
                            )
                            : JobGenerator.GenerateDayOff(
                                context.StateId,
                                participantId,
                                need.Action,
                                roomId,
                                day,
                                context.WorldId,
                                need.Hours
                            )
                    );
                }
            }
        }
    }

    // The group's own home is always an available option (at the same weight any house would carry), uncapped by
    // the shared public-building capacity pool — a family is never turned away from their own front door.
    private static Guid? PickDayOffRoom(
        DayOffNeed need,
        IReadOnlyList<IdleCandidate> cityIdleCandidates,
        List<int> remainingCapacity
    )
    {
        var eligibleIndices = Enumerable
            .Range(0, cityIdleCandidates.Count)
            .Where(i => remainingCapacity[i] >= need.ParticipantIds.Count)
            .Where(i =>
                !need.ExcludeTavern || cityIdleCandidates[i].BuildingType != BuildingType.Tavern
            )
            .ToArray();

        var weights = eligibleIndices.Select(i => cityIdleCandidates[i].Weight).ToList();
        weights.Add(BuildingGenerator.Popularity[BuildingType.House]);

        var pickedIndex = WeightedSampler.SampleIndex(weights);
        if (pickedIndex == eligibleIndices.Length)
        {
            return need.HomeRoomId;
        }

        var candidateIndex = eligibleIndices[pickedIndex];
        remainingCapacity[candidateIndex] -= need.ParticipantIds.Count;
        return cityIdleCandidates[candidateIndex].RoomId;
    }

    internal record IdleCandidate(
        Guid? RoomId,
        Guid DistrictId,
        int Capacity,
        int Weight,
        BuildingType? BuildingType
    );

    internal record HouseholdResult(
        IReadOnlyList<CreatureGeneratorResult> Members,
        IReadOnlyList<IReadOnlyList<Guid>> BedroomGroups,
        IReadOnlyList<Relationship> Relationships
    );
}
