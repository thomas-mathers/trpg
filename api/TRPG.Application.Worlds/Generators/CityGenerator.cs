using TRPG.Application.Buildings;
using TRPG.Domain.Models;

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
    HouseholdGenerator householdGenerator,
    CreatureGroupGenerator creatureGroupGenerator
)
{
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
                    EmploymentAssigner.DistrictPopularity[d.DistrictType],
                    null
                ))
                .ToList(),
        };

        foreach (var type in ShopBuildingTypes.All)
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

            if (type == BuildingType.GuildHall)
            {
                if (_guildHallIndex >= input.NamedFactions.Count)
                {
                    continue;
                }

                GenerateGuildHallBuilding(workspace, district);
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
            StaffingPolicy.GetProfessionForBuilding(type)
        );

        RegisterHousehold(workspace, ownerHousehold);

        var owner = ownerHousehold.DesignatedWorker!;
        var memberIds = new List<Guid> { owner.Id };

        var buildingName = SettlementNameGenerator.GenerateBuildingName(
            input.DominantRace,
            type,
            workspace.HouseholdInput.UsedBuildingNames
        );

        var spec = BuildingSpecCatalog.GetSpecs(type, owner.Id, memberIds, bedroomGroups: null);

        var buildingResult = buildingGenerator.Generate(
            new BuildingGeneratorInput(input.LocationsById[district.LocationId], spec)
            {
                Name = buildingName,
                MemberIds = memberIds,
            }
        );

        workspace.Items.AddRange(buildingResult.KeyItems);
        workspace.DoorConnectorKeys.AddRange(buildingResult.DoorConnectorKeys);

        var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);

        if (spec.IdleDestination is { } idleDestination)
        {
            workspace.IdleCandidates.Add(
                new IdleCandidate(
                    groundFloorRoom.LocationId,
                    groundFloorRoom.Capacity,
                    idleDestination.Popularity,
                    type,
                    idleDestination.OpenHours
                )
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

    private void GenerateGuildHallBuilding(CityWorkspace workspace, District district)
    {
        var input = workspace.Input;

        var ownerCreatures = creatureGroupGenerator.Generate(
            new CreatureGroupGeneratorInput(
                input.DominantRace,
                StaffingPolicy.GetProfessionForBuilding(BuildingType.GuildHall),
                input.WorldId,
                district.LocationId,
                Count: 1,
                MinLevel: 1,
                MaxLevel: 20
            )
        );
        var owner = ownerCreatures[0].Creature;

        var numMembers = Random.Shared.Next(
            input.GeneratorInput.MinFactionMembers,
            input.GeneratorInput.MaxFactionMembers + 1
        );
        var memberCreatures = creatureGroupGenerator.Generate(
            new CreatureGroupGeneratorInput(
                input.DominantRace,
                Profession.Mercenary,
                input.WorldId,
                district.LocationId,
                numMembers - 1,
                MinLevel: 5,
                MaxLevel: 100
            )
        );
        var memberList = memberCreatures.Select(m => m.Creature).ToList();
        var memberIds = new List<Guid> { owner.Id };

        memberIds.AddRange(memberList.Select(m => m.Id));

        var buildingName = SettlementNameGenerator.GenerateBuildingName(
            input.DominantRace,
            BuildingType.GuildHall,
            workspace.HouseholdInput.UsedBuildingNames
        );

        var spec = BuildingSpecCatalog.GetSpecs(
            BuildingType.GuildHall,
            owner.Id,
            memberIds,
            bedroomGroups: null
        );

        var buildingResult = buildingGenerator.Generate(
            new BuildingGeneratorInput(input.LocationsById[district.LocationId], spec)
            {
                Name = buildingName,
                MemberIds = memberIds,
            }
        );

        workspace.Items.AddRange(buildingResult.KeyItems);
        workspace.DoorConnectorKeys.AddRange(buildingResult.DoorConnectorKeys);

        var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);

        var guildFactionId = input.NamedFactions[_guildHallIndex++].Id;
        buildingResult.Building.FactionId = guildFactionId;

        var registration = GuildHallOccupantGenerator.Generate(
            new GuildHallOccupantGeneratorInput
            {
                WorldId = input.WorldId,
                CityFactionId = workspace.CityFaction.Id,
                GuildFactionId = guildFactionId,
                Owner = owner,
                GroundFloorLocationId = groundFloorRoom.LocationId,
                Beds = buildingResult.Props.OfType<Bed>().ToList(),
                Members = memberList,
            }
        );

        workspace.FactionMembers.AddRange(registration.FactionMembers);
        workspace.Jobs.AddRange(registration.Jobs);
        workspace.Creatures.Add(owner);
        workspace.Creatures.AddRange(memberList);
        workspace.Items.AddRange(ownerCreatures.Concat(memberCreatures).SelectMany(m => m.Items));
        workspace.Skills.AddRange(ownerCreatures.Concat(memberCreatures).SelectMany(m => m.Skills));
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

    private static void RegisterShopStaffing(
        CityWorkspace workspace,
        BuildingGeneratorResult buildingResult,
        Guid ownerId
    )
    {
        var type = buildingResult.Building.BuildingType;
        var groundFloorLocationId = buildingResult.Rooms.First(r => r.FloorNumber == 0).LocationId;

        StaffingSchedule schedule;
        if (type == BuildingType.Inn)
        {
            schedule = InnStaffingPolicy.Generate();
        }
        else
        {
            var staffableWorkstationCount = Math.Max(
                1,
                buildingResult
                    .Props.OfType<Workstation>()
                    .Count(w => w.WorkstationType != WorkstationType.Reading)
            );
            schedule = ShopStaffingPolicy.Generate(type, staffableWorkstationCount);
        }

        AssignShiftsToWorkspace(workspace, schedule, ownerId, groundFloorLocationId);
    }

    private static void AssignShiftsToWorkspace(
        CityWorkspace workspace,
        StaffingSchedule schedule,
        Guid ownerId,
        Guid groundFloorLocationId
    )
    {
        var worldId = workspace.Input.WorldId;

        workspace.Jobs.Add(
            CreatureJobGenerator.GenerateWork(
                ownerId,
                groundFloorLocationId,
                worldId,
                schedule.OwnerShift.WorkHours
            )
        );
        CreatureJobGenerator.ApplySleepOverride(
            ownerId,
            schedule.OwnerShift.WorkHours,
            worldId,
            workspace.Jobs
        );
        workspace.ShopOwnerAssignments.Add(
            new StaffDayOff(ownerId, schedule.OwnerShift.DaysOff, schedule.OwnerShift.WorkHours)
        );

        foreach (var shift in schedule.EmployeeShifts)
        {
            workspace.OpenShopSlots.Add(
                new ShopEmploymentSlot(
                    groundFloorLocationId,
                    shift.Profession,
                    shift.DaysOff,
                    shift.WorkHours
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
