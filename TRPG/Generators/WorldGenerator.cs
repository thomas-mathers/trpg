using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Models;

namespace TRPG.Generators;

internal class WorldGeneratorInput {
    public required string Description { get; init; }
    public int FactionCount { get; init; }
    public int HousesPerCity { get; init; }
    public int MaxBuildingsPerState { get; init; }
    public int MaxFactionMembers { get; init; }
    public int MaxHouseholdSize { get; init; }
    public int MinBuildingsPerState { get; init; }
    public int MinFactionMembers { get; init; }
    public int MinHouseholdSize { get; init; }
    public int MaxRuralStates { get; init; }
    public int MaxCityStates { get; init; }
    public int MaxCountries { get; init; }
    public int MinRuralStates { get; init; }
    public int MinCityStates { get; init; }
    public int MinCountries { get; init; }
    public int RaceCount { get; init; }
}

internal class WorldGeneratorResult {
    public required IReadOnlyCollection<PersonAbility> Abilities { get; init; }
    public required IReadOnlyList<BuildingOwner> BuildingOwners { get; init; }
    public required IReadOnlyList<Building> Buildings { get; init; }
    public required IReadOnlyList<State> States { get; init; }
    public required IReadOnlyList<City> Cities { get; init; }
    public required IReadOnlyList<District> Districts { get; init; }
    public required IReadOnlyList<Country> Countries { get; init; }
    public required IReadOnlyList<FactionMember> FactionMembers { get; init; }
    public required IReadOnlyList<Faction> Factions { get; init; }
    public required IReadOnlyList<InventoryItem> InventoryItems { get; init; }
    public required IReadOnlyList<Item> Items { get; init; }
    public required IReadOnlyList<Person> Persons { get; init; }
    public required IReadOnlyList<Prop> Props { get; init; }
    public required IReadOnlyList<Race> Races { get; init; }
    public required IReadOnlyList<Road> Roads { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyCollection<PersonSkill> Skills { get; init; }
    public required World World { get; init; }
}

internal class WorldGenerator(
    BuildingGenerator buildingGenerator,
    FactionsGenerator factionsGenerator,
    GeographyGenerator geographyGenerator,
    PersonGenerator personGenerator,
    RaceGenerator raceGenerator,
    ILogger<WorldGenerator> logger
) {
    private static readonly BuildingType[] StandardBuildingTypes = [
        BuildingType.ArcaneShop, BuildingType.Apothecary, BuildingType.Bakery,
        BuildingType.Blacksmith, BuildingType.Castle, BuildingType.GeneralGoods,
        BuildingType.GuildHall, BuildingType.Jail, BuildingType.Library,
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

        var groundedDescription = $"{generatorInput.Description} This remains a low-fantasy world where knights, "
            + "mercenaries, blacksmiths, and mages are established, respected roles, and swords and plate armor are "
            + "still standard equipment. Any technological or aesthetic theme should layer on top of this as mood "
            + "and atmosphere — not replace or obsolete these roles and equipment.";

        var races = await raceGenerator.Generate(
            new RaceGeneratorInput {
                WorldId = worldId,
                Description = groundedDescription,
                Count = generatorInput.RaceCount
            },
            cancellationToken
        );

        var factions = await factionsGenerator.Generate(
            new FactionsGeneratorInput {
                WorldId = worldId,
                Description = groundedDescription,
                Count = generatorInput.FactionCount
            },
            cancellationToken
        );

        var geography = await geographyGenerator.Generate(
            new GeographyGeneratorInput {
                WorldId = worldId,
                Description = groundedDescription,
                Races = races,
                MaxRuralStates = generatorInput.MaxRuralStates,
                MaxCityStates = generatorInput.MaxCityStates,
                MaxCountries = generatorInput.MaxCountries,
                MinRuralStates = generatorInput.MinRuralStates,
                MinCityStates = generatorInput.MinCityStates,
                MinCountries = generatorInput.MinCountries
            },
            cancellationToken
        );

        var buildings = new List<Building>();
        var persons = new List<Person>();
        var buildingOwners = new List<BuildingOwner>();
        var factionMembers = new List<FactionMember>();
        var items = new List<Item>();
        var inventoryItems = new List<InventoryItem>();
        var rooms = new List<Room>();
        var props = new List<Prop>();
        var skills = new List<PersonSkill>();
        var abilities = new List<PersonAbility>();
        var districts = new List<District>();
        var guildHallIndex = 0;

        var stateById = geography.States.ToDictionary(s => s.Id);

        foreach (var city in geography.Cities) {
            var state = stateById[city.StateId];
            var cityDistricts = Enum.GetValues<DistrictType>()
                .Select(type => DistrictGenerator.Generate(type, city.Id))
                .ToDictionary(d => d.DistrictType);
            districts.AddRange(cityDistricts.Values);

            var cityBuildings = new List<Building>();

            foreach (var type in StandardBuildingTypes) {
                var district = cityDistricts[DistrictGenerator.DistrictTypeByBuildingType[type]];

                var race = races[Random.Shared.Next(races.Count)];
                var personResult = personGenerator.Generate(
                    new PersonGeneratorInput(race, GetProfessionForBuilding(type), worldId, state.Id, state.Id)
                );
                personResult.Person.CityId = city.Id;
                personResult.Person.DistrictId = district.Id;

                var memberIds = new List<Guid> { personResult.Person.Id };
                var memberPersons = new List<PersonGeneratorResult>();

                if (type == BuildingType.GuildHall) {
                    var numMembers = Random.Shared.Next(generatorInput.MinFactionMembers, generatorInput.MaxFactionMembers + 1);
                    for (var m = 1; m < numMembers; m++) {
                        var memberRace = races[Random.Shared.Next(races.Count)];
                        var memberPerson = personGenerator.Generate(
                            new PersonGeneratorInput(memberRace, Profession.Mercenary, worldId, state.Id, state.Id)
                        );
                        memberPerson.Person.CityId = city.Id;
                        memberPerson.Person.DistrictId = district.Id;
                        memberPersons.Add(memberPerson);
                        memberIds.Add(memberPerson.Person.Id);
                    }
                }

                var buildingResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(StateId: state.Id, CityId: city.Id, DistrictId: district.Id,
                        OwnerId: personResult.Person.Id, Type: type) {
                        MemberIds = memberIds
                    }
                );

                if (type == BuildingType.GuildHall) {
                    var factionId = factions[guildHallIndex++ % factions.Count].Id;
                    buildingResult.Building.FactionId = factionId;
                    factionMembers.Add(new FactionMember
                        { FactionId = factionId, PersonId = personResult.Person.Id, Role = FactionRole.Leader });
                    foreach (var memberPerson in memberPersons) {
                        factionMembers.Add(new FactionMember
                            { FactionId = factionId, PersonId = memberPerson.Person.Id, Role = FactionRole.Member });
                        memberPerson.Person.RoomId = buildingResult.Rooms.First(r => r.FloorNumber == 0).Id;
                        persons.Add(memberPerson.Person);
                        items.AddRange(memberPerson.Items);
                        inventoryItems.AddRange(memberPerson.InventoryItems);
                        skills.AddRange(memberPerson.Skills);
                        abilities.AddRange(memberPerson.Abilities);
                    }
                }

                personResult.Person.RoomId = buildingResult.Rooms.First(r => r.FloorNumber == 0).Id;
                cityBuildings.Add(buildingResult.Building);
                persons.Add(personResult.Person);
                items.AddRange(personResult.Items);
                inventoryItems.AddRange(personResult.InventoryItems);
                skills.AddRange(personResult.Skills);
                abilities.AddRange(personResult.Abilities);
                buildingOwners.Add(new BuildingOwner
                    { BuildingId = buildingResult.Building.Id, OwnerId = personResult.Person.Id });
                rooms.AddRange(buildingResult.Rooms);
                props.AddRange(buildingResult.Props);
            }

            var residentialDistrict = cityDistricts[DistrictType.Residential];
            var districtIds = cityDistricts.Values.Select(d => d.Id).ToArray();
            var houseNames = new Queue<string>(BuildingGenerator.Names[BuildingType.House].Shuffle().Take(generatorInput.HousesPerCity));

            for (var h = 0; h < generatorInput.HousesPerCity; h++) {
                var householdSize = Random.Shared.Next(generatorInput.MinHouseholdSize, generatorInput.MaxHouseholdSize + 1);
                var household = new List<PersonGeneratorResult>();
                for (var m = 0; m < householdSize; m++) {
                    var race = races[Random.Shared.Next(races.Count)];
                    var member = personGenerator.Generate(
                        new PersonGeneratorInput(race, GetProfessionForBuilding(BuildingType.House), worldId, state.Id, state.Id)
                    );
                    member.Person.CityId = city.Id;
                    household.Add(member);
                }

                var owner = household[0];
                var houseResult = buildingGenerator.Generate(
                    new BuildingGeneratorInput(StateId: state.Id, CityId: city.Id, DistrictId: residentialDistrict.Id,
                        OwnerId: owner.Person.Id, Type: BuildingType.House) {
                        Name = houseNames.Dequeue()
                    }
                );

                var homeRoomId = houseResult.Rooms.First(r => r.FloorNumber == 0).Id;
                var placedOutdoors = household.Select(_ => Random.Shared.Next(2) == 0).ToArray();
                if (householdSize > 1 && !placedOutdoors.Contains(true)) {
                    placedOutdoors[Random.Shared.Next(placedOutdoors.Length)] = true;
                }

                for (var m = 0; m < household.Count; m++) {
                    var member = household[m];
                    if (placedOutdoors[m]) {
                        member.Person.RoomId = null;
                        member.Person.DistrictId = districtIds[Random.Shared.Next(districtIds.Length)];
                    } else {
                        member.Person.RoomId = homeRoomId;
                        member.Person.DistrictId = residentialDistrict.Id;
                    }

                    persons.Add(member.Person);
                    items.AddRange(member.Items);
                    inventoryItems.AddRange(member.InventoryItems);
                    skills.AddRange(member.Skills);
                    abilities.AddRange(member.Abilities);
                }

                cityBuildings.Add(houseResult.Building);
                buildingOwners.Add(new BuildingOwner { BuildingId = houseResult.Building.Id, OwnerId = owner.Person.Id });
                rooms.AddRange(houseResult.Rooms);
                props.AddRange(houseResult.Props);
            }

            buildings.AddRange(cityBuildings);
        }

        foreach (var state in geography.States) {
            var count = Random.Shared.Next(generatorInput.MinBuildingsPerState, generatorInput.MaxBuildingsPerState + 1);
            var usedNames = new HashSet<string>();
            for (var i = 0; i < count; i++) {
                var result = DungeonGenerator.Generate(new DungeonGeneratorInput(state.Id, usedNames));
                usedNames.Add(result.Building.Name);
                buildings.Add(result.Building);
                rooms.Add(result.Room);
            }
        }

        logger.LogDebug("GenerateWorld completed in {ElapsedSeconds:F1}s", sw.Elapsed.TotalSeconds);

        return new WorldGeneratorResult {
            World = geography.World,
            Countries = geography.Countries,
            States = geography.States,
            Cities = geography.Cities,
            Districts = districts,
            Roads = geography.Roads,
            Races = races,
            Factions = factions,
            Buildings = buildings,
            Persons = persons,
            BuildingOwners = buildingOwners,
            FactionMembers = factionMembers,
            Items = items,
            InventoryItems = inventoryItems,
            Rooms = rooms,
            Props = props,
            Skills = skills,
            Abilities = abilities
        };
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
            _ => Profession.Merchant
        };
    }
}