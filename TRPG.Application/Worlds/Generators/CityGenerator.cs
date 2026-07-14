using TRPG.Application.Game;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class CityGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required City City { get; init; }
    public required State State { get; init; }
    public required CreatureType DominantRace { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyList<Faction> NamedFactions { get; init; }
    public required WorldGeneratorInput GeneratorInput { get; init; }
}

public class CityGeneratorResult
{
    public required Faction CityFaction { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<InventoryItem> InventoryItems { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<CreatureSkill> Skills { get; init; }
    public required IReadOnlyList<CreatureAbility> Abilities { get; init; }
    public required IReadOnlyList<Job> Jobs { get; init; }
    public required IReadOnlyList<RoomConnectorKey> RoomConnectorKeys { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
}

public class CityGenerator(
    BuildingGenerator buildingGenerator,
    CreatureGenerator creatureGenerator,
    HouseholdGenerator householdGenerator
)
{
    private const int AdultAge = 18;
    private const int MaxAdultBirthYear = GameClock.EpochYear - AdultAge;

    private int _guildHallIndex;

    private sealed class CityWorkspace
    {
        public required CityGeneratorInput Input { get; init; }
        public required Faction CityFaction { get; init; }
        public required Dictionary<DistrictType, District> DistrictsByType { get; init; }
        public required HouseholdGeneratorInput HouseholdInput { get; init; }
        public required List<IdleCandidate> IdleCandidates { get; init; }
        public List<Building> Buildings { get; } = [];
        public List<Creature> Creatures { get; } = [];
        public List<BuildingOwner> BuildingOwners { get; } = [];
        public List<FactionMember> FactionMembers { get; } = [];
        public List<Item> Items { get; } = [];
        public List<InventoryItem> InventoryItems { get; } = [];
        public List<Room> Rooms { get; } = [];
        public List<Prop> Props { get; } = [];
        public List<CreatureSkill> Skills { get; } = [];
        public List<CreatureAbility> Abilities { get; } = [];
        public List<Job> Jobs { get; } = [];
        public List<RoomConnectorKey> RoomConnectorKeys { get; } = [];
        public List<Relationship> Relationships { get; } = [];
        public List<ShopEmploymentSlot> OpenShopSlots { get; } = [];
        public List<StaffDayOff> ShopOwnerAssignments { get; } = [];
        public List<Creature> EligibleForEmployment { get; } = [];
        public Dictionary<Guid, List<Creature>> HouseholdByMemberId { get; } = [];
        public Dictionary<Guid, Guid> HomeRoomIdByMemberId { get; } = [];
        public HashSet<Guid> FatherIds { get; } = [];
    }

    private record GuildHallOccupants(
        Creature Owner,
        IReadOnlyList<CreatureGeneratorResult> Members
    );

    public CityGeneratorResult Generate(CityGeneratorInput input)
    {
        var cityFaction = new Faction
        {
            WorldId = input.WorldId,
            Name = $"The People of {input.City.Name}",
            Description = $"The common folk of {input.City.Name}.",
            IsCityFaction = true,
        };

        var districtsByType = input.Districts.ToDictionary(d => d.DistrictType);
        var usedBuildingNames = new HashSet<string>();
        var workspace = new CityWorkspace
        {
            Input = input,
            CityFaction = cityFaction,
            DistrictsByType = districtsByType,
            HouseholdInput = new HouseholdGeneratorInput
            {
                WorldId = input.WorldId,
                City = input.City,
                StateId = input.State.Id,
                ResidentialDistrict = districtsByType[DistrictType.Residential],
                DominantRace = input.DominantRace,
                GeneratorInput = input.GeneratorInput,
                UsedBuildingNames = usedBuildingNames,
            },
            IdleCandidates = input
                .Districts.Select(d => new IdleCandidate(
                    null,
                    d.Id,
                    int.MaxValue,
                    DistrictGenerator.Popularity[d.DistrictType],
                    null
                ))
                .ToList(),
        };

        foreach (var type in ShopStaffingPolicy.StandardBuildingTypes)
        {
            if (
                !districtsByType.TryGetValue(
                    DistrictGenerator.DistrictTypeByBuildingType[type],
                    out var district
                )
            )
            {
                continue;
            }

            if (type == BuildingType.GuildHall && _guildHallIndex >= input.NamedFactions.Count)
            {
                continue;
            }

            GenerateStandardBuilding(workspace, type, district);
        }

        for (var h = 0; h < input.GeneratorInput.HousesPerCity; h++)
        {
            RegisterHousehold(
                workspace,
                householdGenerator.Generate(workspace.HouseholdInput, null)
            );
        }

        EmploymentAssigner.AssignEmployment(
            new CityEmploymentContext
            {
                OpenShopSlots = workspace.OpenShopSlots,
                EligibleForEmployment = workspace.EligibleForEmployment,
                ShopOwnerAssignments = workspace.ShopOwnerAssignments,
                HouseholdByMemberId = workspace.HouseholdByMemberId,
                HomeRoomIdByMemberId = workspace.HomeRoomIdByMemberId,
                FatherIds = workspace.FatherIds,
                CityIdleCandidates = workspace.IdleCandidates,
                StateId = input.State.Id,
                WorldId = input.WorldId,
                Jobs = workspace.Jobs,
            }
        );

        return new CityGeneratorResult
        {
            CityFaction = cityFaction,
            Buildings = workspace.Buildings.ToArray(),
            Creatures = workspace.Creatures.ToArray(),
            BuildingOwners = workspace.BuildingOwners.ToArray(),
            FactionMembers = workspace.FactionMembers.ToArray(),
            Items = workspace.Items.ToArray(),
            InventoryItems = workspace.InventoryItems.ToArray(),
            Rooms = workspace.Rooms.ToArray(),
            Props = workspace.Props.ToArray(),
            Skills = workspace.Skills.ToArray(),
            Abilities = workspace.Abilities.ToArray(),
            Jobs = workspace.Jobs.ToArray(),
            RoomConnectorKeys = workspace.RoomConnectorKeys.ToArray(),
            Relationships = workspace.Relationships.ToArray(),
        };
    }

    private void GenerateStandardBuilding(
        CityWorkspace workspace,
        BuildingType type,
        District district
    )
    {
        var input = workspace.Input;
        var ownerHousehold = householdGenerator.Generate(
            workspace.HouseholdInput,
            ShopStaffingPolicy.GetProfessionForBuilding(type)
        );
        RegisterHousehold(workspace, ownerHousehold);
        var owner = ownerHousehold.DesignatedWorker!;

        IReadOnlyList<CreatureGeneratorResult> memberCreatures =
            type == BuildingType.GuildHall ? GenerateGuildMembers(workspace, district) : [];
        var memberIds = new List<Guid> { owner.Id };
        memberIds.AddRange(memberCreatures.Select(m => m.Creature.Id));

        var isLockable = type is not (BuildingType.Inn or BuildingType.Tavern);
        var buildingName = SettlementNameGenerator.GenerateBuildingName(
            input.DominantRace,
            type,
            workspace.HouseholdInput.UsedBuildingNames
        );

        var buildingResult = buildingGenerator.Generate(
            new BuildingGeneratorInput(
                input.State.Id,
                input.City.Id,
                district.Id,
                owner.Id,
                type,
                input.WorldId
            )
            {
                Name = buildingName,
                MemberIds = memberIds,
                IsLockable = isLockable,
            }
        );

        if (isLockable)
        {
            DistributeBuildingKeys(workspace, buildingResult, memberIds);
        }

        var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);
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
            workspace.IdleCandidates.Add(
                new IdleCandidate(
                    groundFloorRoom.Id,
                    district.Id,
                    groundFloorRoom.Capacity,
                    BuildingGenerator.Popularity[type],
                    type,
                    type == BuildingType.Inn
                        ? null
                        : ShopStaffingPolicy.GetWorkHoursForBuilding(type)
                )
            );
        }

        if (type == BuildingType.GuildHall)
        {
            RegisterGuildFaction(
                workspace,
                buildingResult,
                new GuildHallOccupants(owner, memberCreatures)
            );
        }

        RegisterShopStaffing(workspace, buildingResult, owner.Id);

        workspace.Buildings.Add(buildingResult.Building);
        workspace.BuildingOwners.Add(
            new BuildingOwner
            {
                BuildingId = buildingResult.Building.Id,
                OwnerId = owner.Id,
                WorldId = input.WorldId,
            }
        );
        workspace.Rooms.AddRange(buildingResult.Rooms);
        workspace.Props.AddRange(buildingResult.Props);
    }

    private IReadOnlyList<CreatureGeneratorResult> GenerateGuildMembers(
        CityWorkspace workspace,
        District district
    )
    {
        var input = workspace.Input;
        var numMembers = Random.Shared.Next(
            input.GeneratorInput.MinFactionMembers,
            input.GeneratorInput.MaxFactionMembers + 1
        );

        var members = new List<CreatureGeneratorResult>();
        for (var m = 1; m < numMembers; m++)
        {
            var memberRace = CreatureGenerator.PickCreatureType(input.DominantRace);
            var memberCreature = creatureGenerator.Generate(
                new CreatureGeneratorInput(
                    memberRace,
                    Profession.Mercenary,
                    input.WorldId,
                    input.State.Id,
                    input.State.Id
                )
                {
                    MaxBirthYear = MaxAdultBirthYear,
                }
            );
            memberCreature.Creature.CityId = input.City.Id;
            memberCreature.Creature.DistrictId = district.Id;
            members.Add(memberCreature);
        }

        return members;
    }

    private static void DistributeBuildingKeys(
        CityWorkspace workspace,
        BuildingGeneratorResult buildingResult,
        IReadOnlyList<Guid> memberIds
    )
    {
        var worldId = workspace.Input.WorldId;
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
            workspace.Items.Add(keyItem);
            workspace.InventoryItems.Add(
                new InventoryItem
                {
                    CreatureId = residentId,
                    ItemId = keyItem.Id,
                    Quantity = 1,
                    WorldId = worldId,
                }
            );
            workspace.RoomConnectorKeys.Add(
                new RoomConnectorKey
                {
                    ItemId = keyItem.Id,
                    RoomConnectorId = frontDoor.Id,
                    WorldId = worldId,
                }
            );
        }
    }

    private void RegisterGuildFaction(
        CityWorkspace workspace,
        BuildingGeneratorResult buildingResult,
        GuildHallOccupants occupants
    )
    {
        var input = workspace.Input;
        var groundFloorRoomId = buildingResult.Rooms.First(r => r.FloorNumber == 0).Id;
        var guildFactionId =
            input.NamedFactions.Count > 0 ? input.NamedFactions[_guildHallIndex++].Id : (Guid?)null;

        if (guildFactionId != null)
        {
            buildingResult.Building.FactionId = guildFactionId;
            workspace.FactionMembers.Add(
                new FactionMember
                {
                    FactionId = guildFactionId.Value,
                    CreatureId = occupants.Owner.Id,
                    Role = FactionRole.Leader,
                    WorldId = input.WorldId,
                }
            );
        }

        foreach (var memberCreature in occupants.Members)
        {
            if (guildFactionId != null)
            {
                workspace.FactionMembers.Add(
                    new FactionMember
                    {
                        FactionId = guildFactionId.Value,
                        CreatureId = memberCreature.Creature.Id,
                        Role = FactionRole.Member,
                        WorldId = input.WorldId,
                    }
                );
            }

            workspace.FactionMembers.Add(
                new FactionMember
                {
                    FactionId = workspace.CityFaction.Id,
                    CreatureId = memberCreature.Creature.Id,
                    Role = FactionRole.Member,
                    WorldId = input.WorldId,
                }
            );
            memberCreature.Creature.RoomId = groundFloorRoomId;
            workspace.Creatures.Add(memberCreature.Creature);
            workspace.Items.AddRange(memberCreature.Items);
            workspace.InventoryItems.AddRange(memberCreature.InventoryItems);
            workspace.Skills.AddRange(memberCreature.Skills);
            workspace.Abilities.AddRange(memberCreature.Abilities);

            var memberBedRoomId = buildingResult
                .Props.OfType<Bed>()
                .First(b => b.AssignedCreatureId == memberCreature.Creature.Id)
                .RoomId;
            workspace.Jobs.AddRange(
                JobGenerator.Generate(
                    input.State.Id,
                    memberCreature.Creature.Id,
                    memberBedRoomId,
                    null,
                    groundFloorRoomId,
                    input.WorldId
                )
            );
        }
    }

    private static void RegisterShopStaffing(
        CityWorkspace workspace,
        BuildingGeneratorResult buildingResult,
        Guid ownerId
    )
    {
        var input = workspace.Input;
        var type = buildingResult.Building.BuildingType;
        var groundFloorRoomId = buildingResult.Rooms.First(r => r.FloorNumber == 0).Id;

        if (type == BuildingType.Inn)
        {
            ShopStaffingPolicy.GenerateInnStaffing(
                input.State.Id,
                input.WorldId,
                ownerId,
                groundFloorRoomId,
                workspace.Jobs,
                workspace.ShopOwnerAssignments,
                workspace.OpenShopSlots
            );
            return;
        }

        if (type == BuildingType.GuildHall)
        {
            workspace.Jobs.Add(
                JobGenerator.GenerateWork(input.State.Id, ownerId, groundFloorRoomId, input.WorldId)
            );
            return;
        }

        var workHours = ShopStaffingPolicy.GetWorkHoursForBuilding(type);
        var sleepHours = ShopStaffingPolicy.GetSleepHoursForBuilding(type);
        workspace.Jobs.Add(
            JobGenerator.GenerateWork(
                input.State.Id,
                ownerId,
                groundFloorRoomId,
                input.WorldId,
                workHours
            )
        );
        JobGenerator.ApplySleepOverride(
            ownerId,
            sleepHours,
            input.State.Id,
            input.WorldId,
            workspace.Jobs
        );

        var staffableWorkstationCount = Math.Max(
            1,
            buildingResult
                .Props.OfType<Workstation>()
                .Count(w => w.WorkstationType != WorkstationType.Reading)
        );

        if (staffableWorkstationCount == 1)
        {
            workspace.ShopOwnerAssignments.Add(
                new StaffDayOff(
                    ownerId,
                    ShopStaffingPolicy.NonOverlappingDayOffPatterns[0],
                    workHours
                )
            );
            workspace.OpenShopSlots.Add(
                new ShopEmploymentSlot(
                    groundFloorRoomId,
                    ShopStaffingPolicy.GetEmployeeProfessionForBuilding(type),
                    ShopStaffingPolicy.NonOverlappingDayOffPatterns[1],
                    workHours,
                    sleepHours
                )
            );
            return;
        }

        var totalStaff = Math.Min(staffableWorkstationCount, ShopStaffingPolicy.MaxShopStaff);
        workspace.ShopOwnerAssignments.Add(
            new StaffDayOff(ownerId, ShopStaffingPolicy.StaffDayOffPatterns[0], workHours)
        );
        for (var position = 1; position < totalStaff; position++)
        {
            workspace.OpenShopSlots.Add(
                new ShopEmploymentSlot(
                    groundFloorRoomId,
                    ShopStaffingPolicy.GetEmployeeProfessionForBuilding(type),
                    ShopStaffingPolicy.StaffDayOffPatterns[position],
                    workHours,
                    sleepHours
                )
            );
        }
    }

    private static void RegisterHousehold(
        CityWorkspace workspace,
        HouseholdGeneratorResult household
    )
    {
        workspace.Relationships.AddRange(household.Relationships);

        var householdCreatures = household.Members.Select(m => m.Creature).ToList();
        foreach (var member in household.Members)
        {
            workspace.HouseholdByMemberId[member.Creature.Id] = householdCreatures;
        }

        if (household.FatherId != null)
        {
            workspace.FatherIds.Add(household.FatherId.Value);
        }

        workspace.Items.AddRange(household.KeyItems);
        workspace.InventoryItems.AddRange(household.KeyInventoryItems);
        workspace.RoomConnectorKeys.AddRange(household.KeyConnectorKeys);
        workspace.Jobs.AddRange(household.Jobs);

        foreach (var member in household.Members)
        {
            workspace.Creatures.Add(member.Creature);
            workspace.FactionMembers.Add(
                new FactionMember
                {
                    FactionId = workspace.CityFaction.Id,
                    CreatureId = member.Creature.Id,
                    Role = FactionRole.Member,
                    WorldId = workspace.Input.WorldId,
                }
            );
            workspace.Items.AddRange(member.Items);
            workspace.InventoryItems.AddRange(member.InventoryItems);
            workspace.Skills.AddRange(member.Skills);
            workspace.Abilities.AddRange(member.Abilities);
            workspace.HomeRoomIdByMemberId[member.Creature.Id] = household.HomeRoomId;
        }

        workspace.EligibleForEmployment.AddRange(household.EligibleForEmployment);

        workspace.Buildings.Add(household.House.Building);
        workspace.BuildingOwners.Add(
            new BuildingOwner
            {
                BuildingId = household.House.Building.Id,
                OwnerId = household.HouseOwnerId,
                WorldId = workspace.Input.WorldId,
            }
        );
        workspace.Rooms.AddRange(household.House.Rooms);
        workspace.Props.AddRange(household.House.Props);
    }
}
