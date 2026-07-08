using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Generators;

internal class WorldGeneratorInput {
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

internal class WorldGeneratorResult {
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

internal class WorldGenerator(
    BuildingGenerator buildingGenerator,
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    CreatureGenerator creatureGenerator,
    ILogger<WorldGenerator> logger
) {
    private const double DominantRaceWeight = 0.7;
    private const double FamilyUnitChance = 0.6;
    private const int MinParentBirthYear = 900;
    private const int MaxParentBirthYear = 949;
    private const int YoungestParentingAge = 18;

    private static readonly BuildingType[] StandardBuildingTypes = [
        BuildingType.ArcaneShop, BuildingType.Apothecary, BuildingType.Bakery, BuildingType.Barracks,
        BuildingType.Blacksmith, BuildingType.Castle, BuildingType.GeneralGoods,
        BuildingType.GuildHall, BuildingType.Inn, BuildingType.Jail, BuildingType.Library,
        BuildingType.Stable, BuildingType.Tavern, BuildingType.Temple
    ];

    public async Task<WorldGeneratorResult> Generate(
        WorldGeneratorInput generatorInput,
        CancellationToken cancellationToken
    ) {
        if (generatorInput.HousesPerCity > BuildingGenerator.Names[BuildingType.House].Length) {
            throw new InvalidOperationException(
                $"HousesPerCity ({generatorInput.HousesPerCity}) cannot exceed the house name pool size ({BuildingGenerator.Names[BuildingType.House].Length}).");
        }

        var sw = Stopwatch.StartNew();
        var worldId = Guid.NewGuid();

        var groundedDescription =
            $"""
            {generatorInput.Description} This remains a low-fantasy world where knights,
            mercenaries, blacksmiths, and mages are established, respected roles, and swords
            and plate armor are still standard equipment. Any technological or aesthetic
            theme should layer on top of this as mood and atmosphere — not replace or
            obsolete these roles and equipment. The peoples of this world are Humans, Elves,
            Dwarves, Orcs, Halflings, and Gnomes — do not invent other playable races (no
            Tieflings, Dragonborn, Elementals, or similar) though monstrous threats like
            undead, demons, and beasts may lurk at the margins of civilization.
            """;

        var factions = (await factionsGenerator.Generate(
            new FactionsGeneratorInput {
                WorldId = worldId,
                Description = groundedDescription,
                Count = generatorInput.FactionCount
            },
            cancellationToken
        )).ToList();
        var namedFactionCount = factions.Count;

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput {
                WorldId = worldId,
                Description = groundedDescription,
                MaxRuralStates = generatorInput.MaxRuralStates,
                MaxCityStates = generatorInput.MaxCityStates,
                MinRuralStates = generatorInput.MinRuralStates,
                MinCityStates = generatorInput.MinCityStates
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
        var districtsByCityId = geography.Districts.GroupBy(d => d.CityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var city in geography.Cities) {
            var state = stateById[city.StateId];
            var dominantRace = geography.DominantRaceByCountryId[city.CountryId];
            var usedBuildingNames = new HashSet<string>();
            var cityDistricts = districtsByCityId[city.Id].ToDictionary(d => d.DistrictType);

            var cityFaction = new Faction {
                WorldId = worldId, Name = $"The People of {city.Name}", Description = $"The common folk of {city.Name}.",
                IsCityFaction = true
            };
            factions.Add(cityFaction);

            var cityBuildings = new List<Building>();
            var cityIdleCandidates = cityDistricts.Values
                .Select(d => new IdleCandidate(null, d.Id, int.MaxValue, DistrictGenerator.Popularity[d.DistrictType]))
                .ToList();
            var pendingIdleMembers = new List<Creature>();

            foreach (var type in StandardBuildingTypes) {
                if (!cityDistricts.TryGetValue(DistrictGenerator.DistrictTypeByBuildingType[type], out var district)) {
                    continue;
                }

                var creatureType = PickCreatureType(dominantRace);
                var creatureResult = creatureGenerator.Generate(
                    new CreatureGeneratorInput(creatureType, GetProfessionForBuilding(type), worldId, state.Id, state.Id)
                );
                creatureResult.Creature.CityId = city.Id;
                creatureResult.Creature.DistrictId = district.Id;

                var memberIds = new List<Guid> { creatureResult.Creature.Id };
                var memberCreatures = new List<CreatureGeneratorResult>();

                if (type == BuildingType.GuildHall) {
                    var numMembers = Random.Shared.Next(generatorInput.MinFactionMembers,
                        generatorInput.MaxFactionMembers + 1);
                    for (var m = 1; m < numMembers; m++) {
                        var memberRace = PickCreatureType(dominantRace);
                        var memberCreature = creatureGenerator.Generate(
                            new CreatureGeneratorInput(memberRace, Profession.Mercenary, worldId, state.Id, state.Id)
                        );
                        memberCreature.Creature.CityId = city.Id;
                        memberCreature.Creature.DistrictId = district.Id;
                        memberCreatures.Add(memberCreature);
                        memberIds.Add(memberCreature.Creature.Id);
                    }
                }

                var isLockable = type is not (BuildingType.Inn or BuildingType.Tavern);
                var buildingName = SettlementNameGenerator.GenerateBuildingName(dominantRace, type, usedBuildingNames);

                var buildingResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(state.Id, city.Id, district.Id,
                        creatureResult.Creature.Id, type, worldId) {
                        Name = buildingName,
                        MemberIds = memberIds,
                        IsLockable = isLockable
                    }
                );

                if (isLockable) {
                    var frontDoor = buildingResult.Props.OfType<RoomConnector>()
                        .First(c => c.DestinationRoomId == null);
                    foreach (var residentId in memberIds) {
                        var keyItem = new Item {
                            WorldId = worldId, Name = $"Key to {buildingResult.Building.Name}",
                            Description = $"A key that unlocks {buildingResult.Building.Name}."
                        };
                        items.Add(keyItem);
                        inventoryItems.Add(new InventoryItem
                            { CreatureId = residentId, ItemId = keyItem.Id, Quantity = 1, WorldId = worldId });
                        roomConnectorKeys.Add(new RoomConnectorKey
                            { ItemId = keyItem.Id, RoomConnectorId = frontDoor.Id, WorldId = worldId });
                    }
                }

                var groundFloorRoom = buildingResult.Rooms.First(r => r.FloorNumber == 0);
                var groundFloorRoomId = groundFloorRoom.Id;
                if (type != BuildingType.Jail) {
                    cityIdleCandidates.Add(new IdleCandidate(
                        groundFloorRoomId, district.Id, groundFloorRoom.Capacity, BuildingGenerator.Popularity[type]));
                }

                if (type == BuildingType.GuildHall) {
                    var guildFactionId = namedFactionCount > 0
                        ? factions[guildHallIndex++ % namedFactionCount].Id
                        : (Guid?) null;

                    if (guildFactionId != null) {
                        buildingResult.Building.FactionId = guildFactionId;
                        factionMembers.Add(new FactionMember {
                            FactionId = guildFactionId.Value, CreatureId = creatureResult.Creature.Id,
                            Role = FactionRole.Leader, WorldId = worldId
                        });
                    }

                    foreach (var memberCreature in memberCreatures) {
                        if (guildFactionId != null) {
                            factionMembers.Add(new FactionMember {
                                FactionId = guildFactionId.Value, CreatureId = memberCreature.Creature.Id,
                                Role = FactionRole.Member, WorldId = worldId
                            });
                        }

                        factionMembers.Add(new FactionMember {
                            FactionId = cityFaction.Id, CreatureId = memberCreature.Creature.Id,
                            Role = FactionRole.Member, WorldId = worldId
                        });
                        memberCreature.Creature.RoomId = groundFloorRoomId;
                        creatures.Add(memberCreature.Creature);
                        items.AddRange(memberCreature.Items);
                        inventoryItems.AddRange(memberCreature.InventoryItems);
                        skills.AddRange(memberCreature.Skills);
                        abilities.AddRange(memberCreature.Abilities);

                        var memberBedRoomId = buildingResult.Props.OfType<Bed>()
                            .First(b => b.AssignedCreatureId == memberCreature.Creature.Id).RoomId;
                        jobs.AddRange(JobGenerator.Generate(state.Id, memberCreature.Creature.Id, memberBedRoomId,
                            null, groundFloorRoomId, worldId));
                    }
                }

                creatureResult.Creature.RoomId = groundFloorRoomId;
                var ownerBedRoomId = buildingResult.Props.OfType<Bed>()
                    .FirstOrDefault(b => b.AssignedCreatureId == creatureResult.Creature.Id)?.RoomId ??
                    groundFloorRoomId;
                jobs.AddRange(JobGenerator.Generate(state.Id, creatureResult.Creature.Id, ownerBedRoomId,
                    groundFloorRoomId, groundFloorRoomId, worldId));

                cityBuildings.Add(buildingResult.Building);
                creatures.Add(creatureResult.Creature);
                factionMembers.Add(new FactionMember {
                    FactionId = cityFaction.Id, CreatureId = creatureResult.Creature.Id, Role = FactionRole.Member,
                    WorldId = worldId
                });
                items.AddRange(creatureResult.Items);
                inventoryItems.AddRange(creatureResult.InventoryItems);
                skills.AddRange(creatureResult.Skills);
                abilities.AddRange(creatureResult.Abilities);
                buildingOwners.Add(new BuildingOwner {
                    BuildingId = buildingResult.Building.Id, OwnerId = creatureResult.Creature.Id, WorldId = worldId
                });
                rooms.AddRange(buildingResult.Rooms);
                props.AddRange(buildingResult.Props);
            }

            var residentialDistrict = cityDistricts[DistrictType.Residential];

            for (var h = 0; h < generatorInput.HousesPerCity; h++) {
                var householdResult = Random.Shared.NextDouble() < FamilyUnitChance
                    ? GenerateFamilyHousehold(dominantRace, worldId, state.Id, generatorInput)
                    : GenerateSingleHousehold(dominantRace, worldId, state.Id);
                var household = householdResult.Members;
                relationships.AddRange(householdResult.Relationships);

                foreach (var member in household) {
                    member.Creature.CityId = city.Id;
                }

                var owner = household[0];
                var houseName =
                    SettlementNameGenerator.GenerateBuildingName(dominantRace, BuildingType.House, usedBuildingNames);
                var houseResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(state.Id, city.Id, residentialDistrict.Id,
                        owner.Creature.Id, BuildingType.House, worldId) {
                        Name = houseName,
                        MemberIds = household.Select(m => m.Creature.Id).ToList(),
                        BedroomGroups = householdResult.BedroomGroups,
                        IsLockable = true
                    }
                );

                var houseFrontDoor = houseResult.Props.OfType<RoomConnector>().First(c => c.DestinationRoomId == null);
                foreach (var resident in household) {
                    var houseKeyItem = new Item {
                        WorldId = worldId, Name = $"Key to {houseResult.Building.Name}",
                        Description = $"A key that unlocks {houseResult.Building.Name}."
                    };
                    items.Add(houseKeyItem);
                    inventoryItems.Add(new InventoryItem {
                        CreatureId = resident.Creature.Id, ItemId = houseKeyItem.Id, Quantity = 1, WorldId = worldId
                    });
                    roomConnectorKeys.Add(new RoomConnectorKey
                        { ItemId = houseKeyItem.Id, RoomConnectorId = houseFrontDoor.Id, WorldId = worldId });
                }

                var homeRoom = houseResult.Rooms.First(r => r.FloorNumber == 0);
                var homeRoomId = homeRoom.Id;
                cityIdleCandidates.Add(new IdleCandidate(
                    homeRoomId, residentialDistrict.Id, homeRoom.Capacity,
                    BuildingGenerator.Popularity[BuildingType.House]));

                foreach (var member in household) {
                    creatures.Add(member.Creature);
                    factionMembers.Add(new FactionMember {
                        FactionId = cityFaction.Id, CreatureId = member.Creature.Id, Role = FactionRole.Member,
                        WorldId = worldId
                    });
                    items.AddRange(member.Items);
                    inventoryItems.AddRange(member.InventoryItems);
                    skills.AddRange(member.Skills);
                    abilities.AddRange(member.Abilities);

                    var memberBedRoomId = houseResult.Props.OfType<Bed>()
                        .First(b => b.AssignedCreatureId == member.Creature.Id).RoomId;
                    jobs.Add(JobGenerator.GenerateSleep(state.Id, member.Creature.Id, memberBedRoomId, worldId));
                    pendingIdleMembers.Add(member.Creature);
                }

                cityBuildings.Add(houseResult.Building);
                buildingOwners.Add(new BuildingOwner
                    { BuildingId = houseResult.Building.Id, OwnerId = owner.Creature.Id, WorldId = worldId });
                rooms.AddRange(houseResult.Rooms);
                props.AddRange(houseResult.Props);
            }

            var remainingCapacity = cityIdleCandidates.Select(c => c.Capacity).ToList();
            foreach (var member in pendingIdleMembers) {
                var availableIndices = Enumerable.Range(0, cityIdleCandidates.Count)
                    .Where(i => remainingCapacity[i] > 0).ToList();

                var weights = availableIndices.Select(i => cityIdleCandidates[i].Weight).ToArray();
                var index = availableIndices[WeightedSampler.SampleIndex(weights)];

                var candidate = cityIdleCandidates[index];
                remainingCapacity[index]--;
                member.RoomId = candidate.RoomId;
                member.DistrictId = candidate.DistrictId;
                jobs.Add(JobGenerator.GenerateIdle(state.Id, member.Id, candidate.RoomId, worldId));
            }

            buildings.AddRange(cityBuildings);
        }

        foreach (var state in geography.States) {
            var count = Random.Shared.Next(generatorInput.MinBuildingsPerState,
                generatorInput.MaxBuildingsPerState + 1);
            var usedNames = new HashSet<string>();
            for (var i = 0; i < count; i++) {
                var result = DungeonGenerator.Generate(new DungeonGeneratorInput(state.Id, usedNames, worldId));
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.Add(result.Room);
            }
        }

        BiographyGenerator.AssignBiographies(
            new BiographyGeneratorInput(creatures, stateById, factionMembers, factions, relationships));

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult {
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
            Relationships = relationships
        };
    }

    private HouseholdResult GenerateFamilyHousehold(CreatureType dominantRace, Guid worldId, Guid stateId,
        WorldGeneratorInput generatorInput) {
        var creatureType = PickCreatureType(dominantRace);
        var lastName = CreatureGenerator.GetLastName(creatureType);
        var profession = GetProfessionForBuilding(BuildingType.House);

        var motherFirstName = CreatureGenerator.GetFirstName(creatureType, Gender.Female);
        var mother = creatureGenerator.Generate(new CreatureGeneratorInput(creatureType, profession, worldId, stateId,
            stateId) {
            Gender = Gender.Female,
            Name = CreatureGenerator.ComposeFullName(creatureType, Gender.Female, motherFirstName, lastName),
            MinBirthYear = MinParentBirthYear,
            MaxBirthYear = MaxParentBirthYear
        });
        var fatherFirstName = CreatureGenerator.GetFirstName(creatureType, Gender.Male);
        var father = creatureGenerator.Generate(new CreatureGeneratorInput(creatureType, profession, worldId, stateId,
            stateId) {
            Gender = Gender.Male,
            Name = CreatureGenerator.ComposeFullName(creatureType, Gender.Male, fatherFirstName, lastName),
            MinBirthYear = MinParentBirthYear,
            MaxBirthYear = MaxParentBirthYear
        });

        var householdSize = Random.Shared.Next(generatorInput.MinHouseholdSize, generatorInput.MaxHouseholdSize + 1);
        var kidCount = Math.Max(0, householdSize - 2);
        var oldestParentBirthYear = Math.Max(mother.Creature.BirthYear, father.Creature.BirthYear);
        var minKidBirthYear = oldestParentBirthYear + YoungestParentingAge;

        var kids = new List<CreatureGeneratorResult>();
        for (var k = 0; k < kidCount; k++) {
            var kidGender = Random.Shared.Next(2) == 0 ? Gender.Male : Gender.Female;
            var kidFirstName = CreatureGenerator.GetFirstName(creatureType, kidGender);
            var kid = creatureGenerator.Generate(new CreatureGeneratorInput(creatureType, profession, worldId,
                stateId, stateId) {
                Gender = kidGender,
                Name = CreatureGenerator.ComposeFullName(creatureType, kidGender, kidFirstName, lastName),
                MinBirthYear = minKidBirthYear,
                MaxBirthYear = GameClock.EpochYear - 1
            });
            kids.Add(kid);
        }

        var relationships = BuildFamilyRelationships(worldId, mother, father, kids);

        var members = new List<CreatureGeneratorResult> { mother, father };
        members.AddRange(kids);

        List<IReadOnlyList<Guid>> bedroomGroups = [[mother.Creature.Id, father.Creature.Id]];
        bedroomGroups.AddRange(kids.Select(kid => (IReadOnlyList<Guid>) [kid.Creature.Id]));

        return new HouseholdResult(members, bedroomGroups, relationships);
    }

    private static IReadOnlyList<Relationship> BuildFamilyRelationships(Guid worldId, CreatureGeneratorResult mother,
        CreatureGeneratorResult father, IReadOnlyList<CreatureGeneratorResult> kids) {
        var relationships = new List<Relationship>();

        relationships.Add(new Relationship {
            SubjectId = mother.Creature.Id, RelativeId = father.Creature.Id, RelationshipType = RelationshipType.Husband,
            WorldId = worldId
        });
        relationships.Add(new Relationship {
            SubjectId = father.Creature.Id, RelativeId = mother.Creature.Id, RelationshipType = RelationshipType.Wife,
            WorldId = worldId
        });

        foreach (var kid in kids) {
            var kidRoleForParent = kid.Creature.Gender == Gender.Male ? RelationshipType.Son : RelationshipType.Daughter;
            relationships.Add(new Relationship {
                SubjectId = kid.Creature.Id, RelativeId = mother.Creature.Id, RelationshipType = RelationshipType.Mother,
                WorldId = worldId
            });
            relationships.Add(new Relationship {
                SubjectId = kid.Creature.Id, RelativeId = father.Creature.Id, RelationshipType = RelationshipType.Father,
                WorldId = worldId
            });
            relationships.Add(new Relationship {
                SubjectId = mother.Creature.Id, RelativeId = kid.Creature.Id, RelationshipType = kidRoleForParent,
                WorldId = worldId
            });
            relationships.Add(new Relationship {
                SubjectId = father.Creature.Id, RelativeId = kid.Creature.Id, RelationshipType = kidRoleForParent,
                WorldId = worldId
            });
        }

        foreach (var kid in kids) {
            foreach (var sibling in kids) {
                if (kid == sibling) {
                    continue;
                }

                relationships.Add(new Relationship {
                    SubjectId = kid.Creature.Id, RelativeId = sibling.Creature.Id,
                    RelationshipType = sibling.Creature.Gender == Gender.Male
                        ? RelationshipType.Brother
                        : RelationshipType.Sister,
                    WorldId = worldId
                });
            }
        }

        return relationships;
    }

    private HouseholdResult GenerateSingleHousehold(CreatureType dominantRace, Guid worldId, Guid stateId) {
        var creatureType = PickCreatureType(dominantRace);
        var member = creatureGenerator.Generate(
            new CreatureGeneratorInput(creatureType, GetProfessionForBuilding(BuildingType.House), worldId, stateId,
                stateId)
        );

        return new HouseholdResult([member], [[member.Creature.Id]], []);
    }

    private static CreatureType PickCreatureType(CreatureType dominantRace) {
        if (Random.Shared.NextDouble() < DominantRaceWeight) {
            return dominantRace;
        }

        var others = CreatureTypes.Humanoid.Where(r => r != dominantRace).ToArray();
        return others[Random.Shared.Next(others.Length)];
    }

    private static Profession GetProfessionForBuilding(BuildingType type) {
        return type switch {
            BuildingType.Tavern => Profession.Bartender,
            BuildingType.Blacksmith => Profession.Blacksmith,
            BuildingType.Temple => Profession.Cleric,
            BuildingType.Library => Profession.Scholar,
            BuildingType.GeneralGoods => Profession.Merchant,
            BuildingType.Apothecary => Profession.Alchemist,
            BuildingType.Bakery => Profession.Merchant,
            BuildingType.Stable => Profession.StableMaster,
            BuildingType.ArcaneShop => Profession.Mage,
            BuildingType.GuildHall => Profession.Politician,
            BuildingType.Castle => Profession.Politician,
            BuildingType.Jail => Profession.Guard,
            BuildingType.Inn => Profession.Merchant,
            BuildingType.Barracks => Profession.Guard,
            _ => Profession.Merchant
        };
    }

    private record IdleCandidate(Guid? RoomId, Guid DistrictId, int Capacity, int Weight);

    private record HouseholdResult(
        IReadOnlyList<CreatureGeneratorResult> Members,
        IReadOnlyList<IReadOnlyList<Guid>> BedroomGroups,
        IReadOnlyList<Relationship> Relationships);
}
