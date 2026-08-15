using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

internal class CityGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required City City { get; init; }
    public required State State { get; init; }
    public required CreatureType DominantRace { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyDictionary<Guid, Location> LocationsById { get; init; }
    public required IReadOnlyList<Faction> NamedFactions { get; init; }
    public required WorldGeneratorInput GeneratorInput { get; init; }
}

internal class CityGeneratorResult
{
    public required Faction CityFaction { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<Creature> Creatures { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyList<Location> Locations { get; init; }
    public required IReadOnlyList<LocationConnector> LocationConnectors { get; init; }
    public required IReadOnlyList<DoorConnector> DoorConnectors { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<CreatureSkill> Skills { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required IReadOnlyList<DoorConnectorKey> DoorConnectorKeys { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
}

public class CityGenerator(
    BuildingGenerator buildingGenerator,
    CreatureGenerator creatureGenerator,
    HouseholdGenerator householdGenerator
)
{
    private const int AdultAge = 18;
    private const int MaxAdultBirthYear = WorldEpoch.Year - AdultAge;

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
        public List<Room> Rooms { get; } = [];
        public List<Location> Locations { get; } = [];
        public List<LocationConnector> LocationConnectors { get; } = [];
        public List<DoorConnector> DoorConnectors { get; } = [];
        public List<Prop> Props { get; } = [];
        public List<CreatureSkill> Skills { get; } = [];
        public List<CreatureJob> Jobs { get; } = [];
        public List<DoorConnectorKey> DoorConnectorKeys { get; } = [];
        public List<Relationship> Relationships { get; } = [];
        public List<ShopEmploymentSlot> OpenShopSlots { get; } = [];
        public List<StaffDayOff> ShopOwnerAssignments { get; } = [];
        public List<Creature> EligibleForEmployment { get; } = [];
        public Dictionary<Guid, List<Creature>> HouseholdByMemberId { get; } = [];
        public Dictionary<Guid, Guid> HomeLocationIdByMemberId { get; } = [];
        public HashSet<Guid> FatherIds { get; } = [];
    }

    private record GuildHallOccupants(
        Creature Owner,
        IReadOnlyList<CreatureGeneratorResult> Members
    );

    internal CityGeneratorResult Generate(CityGeneratorInput input)
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
                ResidentialDistrict = districtsByType[DistrictType.Residential],
                ResidentialLocation = input.LocationsById[
                    districtsByType[DistrictType.Residential].LocationId
                ],
                DominantRace = input.DominantRace,
                GeneratorInput = input.GeneratorInput,
                UsedBuildingNames = usedBuildingNames,
            },
            IdleCandidates = input
                .Districts.Where(d => d.DistrictType != DistrictType.CityEntrance)
                .Select(d => new IdleCandidate(
                    d.LocationId,
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
                HomeLocationIdByMemberId = workspace.HomeLocationIdByMemberId,
                FatherIds = workspace.FatherIds,
                CityIdleCandidates = workspace.IdleCandidates,
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
            Rooms = workspace.Rooms.ToArray(),
            Locations = workspace.Locations.ToArray(),
            LocationConnectors = workspace.LocationConnectors.ToArray(),
            DoorConnectors = workspace.DoorConnectors.ToArray(),
            Props = workspace.Props.ToArray(),
            Skills = workspace.Skills.ToArray(),
            Jobs = workspace.Jobs.ToArray(),
            DoorConnectorKeys = workspace.DoorConnectorKeys.ToArray(),
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
            new BuildingGeneratorInput(input.LocationsById[district.LocationId], owner.Id, type)
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
                    groundFloorRoom.LocationId,
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
        workspace.Locations.AddRange(buildingResult.Locations);
        workspace.Props.AddRange(buildingResult.Props);
        workspace.LocationConnectors.AddRange(buildingResult.LocationConnectors);
        workspace.DoorConnectors.Add(buildingResult.FrontDoor);
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
                    CreatureArchetype.For(Profession.Mercenary),
                    input.WorldId,
                    district.LocationId,
                    MinLevel: 5,
                    MaxLevel: 100
                )
                {
                    MaxBirthYear = MaxAdultBirthYear,
                }
            );
            memberCreature.Creature.LocationId = district.LocationId;
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
        var frontDoor = buildingResult.FrontDoor;
        foreach (var residentId in memberIds)
        {
            var keyItem = new Key
            {
                WorldId = worldId,
                Name = $"Key to {buildingResult.Building.Name}",
                Description = $"A key that unlocks {buildingResult.Building.Name}.",
                Quantity = 1,
                Ownership = new ItemOwnership
                {
                    OwnerId = residentId,
                    OwnerType = OwnerType.Creature,
                },
            };
            workspace.Items.Add(keyItem);
            workspace.DoorConnectorKeys.Add(
                new DoorConnectorKey
                {
                    ItemId = keyItem.Id,
                    DoorConnectorId = frontDoor.Id,
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
        var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);
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
            memberCreature.Creature.LocationId = groundFloorRoom.LocationId;
            workspace.Creatures.Add(memberCreature.Creature);
            workspace.Items.AddRange(memberCreature.Items);
            workspace.Skills.AddRange(memberCreature.Skills);

            var memberBedLocationId = buildingResult
                .Props.OfType<Bed>()
                .First(b => b.AssignedCreatureId == memberCreature.Creature.Id)
                .LocationId;
            workspace.Jobs.AddRange(
                CreatureJobGenerator.Generate(
                    memberCreature.Creature.Id,
                    memberBedLocationId,
                    null,
                    groundFloorRoom.LocationId,
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
        var groundFloorLocationId = buildingResult.Rooms.First(r => r.FloorNumber == 0).LocationId;

        if (type == BuildingType.Inn)
        {
            ShopStaffingPolicy.GenerateInnStaffing(
                input.WorldId,
                ownerId,
                groundFloorLocationId,
                workspace.Jobs,
                workspace.ShopOwnerAssignments,
                workspace.OpenShopSlots
            );
            return;
        }

        if (type == BuildingType.GuildHall)
        {
            workspace.Jobs.Add(
                CreatureJobGenerator.GenerateWork(ownerId, groundFloorLocationId, input.WorldId)
            );
            return;
        }

        var workHours = ShopStaffingPolicy.GetWorkHoursForBuilding(type);
        var sleepHours = ShopStaffingPolicy.GetSleepHoursForBuilding(type);
        workspace.Jobs.Add(
            CreatureJobGenerator.GenerateWork(
                ownerId,
                groundFloorLocationId,
                input.WorldId,
                workHours
            )
        );
        CreatureJobGenerator.ApplySleepOverride(ownerId, sleepHours, input.WorldId, workspace.Jobs);

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
                    groundFloorLocationId,
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
                    groundFloorLocationId,
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
        workspace.DoorConnectorKeys.AddRange(household.DoorConnectorKeys);
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
            workspace.Skills.AddRange(member.Skills);
            workspace.HomeLocationIdByMemberId[member.Creature.Id] = household.HomeLocationId;
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
        workspace.Locations.AddRange(household.House.Locations);
        workspace.Props.AddRange(household.House.Props);
        workspace.LocationConnectors.AddRange(household.House.LocationConnectors);
        workspace.DoorConnectors.Add(household.House.FrontDoor);
    }
}
