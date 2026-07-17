using TRPG.Application.GameSessions;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Generators;

public class HouseholdGeneratorInput
{
    public required Guid WorldId { get; init; }
    public required City City { get; init; }
    public required Guid StateId { get; init; }
    public required District ResidentialDistrict { get; init; }
    public required CreatureType DominantRace { get; init; }
    public required WorldGeneratorInput GeneratorInput { get; init; }
    public required HashSet<string> UsedBuildingNames { get; init; }
}

public class HouseholdGeneratorResult
{
    public required IReadOnlyList<CreatureGeneratorResult> Members { get; init; }
    public required IReadOnlyList<Relationship> Relationships { get; init; }
    public required BuildingGeneratorResult House { get; init; }
    public required Guid HouseOwnerId { get; init; }
    public required Guid HomeRoomId { get; init; }
    public required IReadOnlyList<Item> KeyItems { get; init; }
    public required IReadOnlyList<InventoryItem> KeyInventoryItems { get; init; }
    public required IReadOnlyList<RoomConnectorKey> KeyConnectorKeys { get; init; }
    public required IReadOnlyList<CreatureJob> Jobs { get; init; }
    public required Creature? DesignatedWorker { get; init; }
    public required Guid? FatherId { get; init; }
    public required IReadOnlyList<Creature> EligibleForEmployment { get; init; }
}

internal record HouseholdResult(
    IReadOnlyList<CreatureGeneratorResult> Members,
    IReadOnlyList<IReadOnlyList<Guid>> BedroomGroups,
    IReadOnlyList<Relationship> Relationships
);

public class HouseholdGenerator(
    BuildingGenerator buildingGenerator,
    CreatureGenerator creatureGenerator
)
{
    private const double FamilyUnitChance = 0.6;
    private const int MinParentBirthYear = 900;
    private const int MaxParentBirthYear = 949;
    private const int YoungestParentingAge = 18;
    private const int AdultAge = 18;
    private const int MaxAdultBirthYear = GameClock.EpochYear - AdultAge;

    public HouseholdGeneratorResult Generate(
        HouseholdGeneratorInput input,
        Profession? designatedProfession
    )
    {
        var householdResult =
            Random.Shared.NextDouble() < FamilyUnitChance
                ? GenerateFamilyHousehold(
                    input.DominantRace,
                    input.WorldId,
                    input.StateId,
                    input.GeneratorInput
                )
                : GenerateSingleHousehold(input.DominantRace, input.WorldId, input.StateId);
        var household = householdResult.Members;

        foreach (var member in household)
        {
            member.Creature.CityId = input.City.Id;
        }

        var fatherId = household.Count >= 2 ? household[1].Creature.Id : (Guid?)null;

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
            input.DominantRace,
            BuildingType.House,
            input.UsedBuildingNames
        );
        var houseResult = buildingGenerator.Generate(
            new BuildingGeneratorInput(
                input.StateId,
                input.City.Id,
                input.ResidentialDistrict.Id,
                houseOwner.Creature.Id,
                BuildingType.House,
                input.WorldId
            )
            {
                Name = houseName,
                MemberIds = household.Select(m => m.Creature.Id).ToList(),
                BedroomGroups = householdResult.BedroomGroups,
                IsLockable = true,
            }
        );

        var keyItems = new List<Item>();
        var keyInventoryItems = new List<InventoryItem>();
        var keyConnectorKeys = new List<RoomConnectorKey>();
        var houseFrontDoor = houseResult
            .Props.OfType<RoomConnector>()
            .First(c => c.DestinationRoomId == null);
        foreach (var resident in household)
        {
            var houseKeyItem = new Item
            {
                WorldId = input.WorldId,
                Name = $"Key to {houseResult.Building.Name}",
                Description = $"A key that unlocks {houseResult.Building.Name}.",
            };
            keyItems.Add(houseKeyItem);
            keyInventoryItems.Add(
                new InventoryItem
                {
                    CreatureId = resident.Creature.Id,
                    ItemId = houseKeyItem.Id,
                    Quantity = 1,
                    WorldId = input.WorldId,
                }
            );
            keyConnectorKeys.Add(
                new RoomConnectorKey
                {
                    ItemId = houseKeyItem.Id,
                    RoomConnectorId = houseFrontDoor.Id,
                    WorldId = input.WorldId,
                }
            );
        }

        var homeRoom = houseResult.Rooms.First(r => r.FloorNumber == 0);
        var homeRoomId = homeRoom.Id;

        var jobs = new List<CreatureJob>();
        foreach (var member in household)
        {
            var memberBedRoomId = houseResult
                .Props.OfType<Bed>()
                .First(b => b.AssignedCreatureId == member.Creature.Id)
                .RoomId;
            jobs.Add(
                CreatureJobGenerator.GenerateSleep(
                    input.StateId,
                    member.Creature.Id,
                    memberBedRoomId,
                    input.WorldId
                )
            );
            jobs.Add(
                CreatureJobGenerator.GenerateIdle(
                    input.StateId,
                    member.Creature.Id,
                    homeRoomId,
                    input.WorldId
                )
            );
            member.Creature.RoomId = homeRoomId;
            member.Creature.DistrictId = input.ResidentialDistrict.Id;
        }

        return new HouseholdGeneratorResult
        {
            Members = household,
            Relationships = householdResult.Relationships,
            House = houseResult,
            HouseOwnerId = houseOwner.Creature.Id,
            HomeRoomId = homeRoomId,
            KeyItems = keyItems.ToArray(),
            KeyInventoryItems = keyInventoryItems.ToArray(),
            KeyConnectorKeys = keyConnectorKeys.ToArray(),
            Jobs = jobs.ToArray(),
            DesignatedWorker = designatedWorker?.Creature,
            FatherId = fatherId,
            EligibleForEmployment = household
                .Select(m => m.Creature)
                .Where(c => c.Profession == Profession.Unemployed)
                .ToArray(),
        };
    }

    internal HouseholdResult GenerateFamilyHousehold(
        CreatureType dominantRace,
        Guid worldId,
        Guid stateId,
        WorldGeneratorInput generatorInput
    )
    {
        var creatureType = CreatureGenerator.PickCreatureType(dominantRace);
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

    internal HouseholdResult GenerateSingleHousehold(
        CreatureType dominantRace,
        Guid worldId,
        Guid stateId
    )
    {
        var creatureType = CreatureGenerator.PickCreatureType(dominantRace);
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
}
