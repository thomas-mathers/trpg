using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.WorldGeneration.Mappers;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class CreateWorldCommand
{
    public required WorldGeneratorInput WorldInput { get; init; }
    public required string Name { get; init; }
    public required PlayerGender Gender { get; init; }
    public required int Age { get; init; }
    public required Race Race { get; init; }
    public required PlayerClass PlayerClass { get; init; }
    public required IReadOnlyDictionary<
        AllocatableAttributeName,
        int
    > StartingAttributeAllocation { get; init; }
}

public record CreateWorldResult(Guid WorldId, Guid PlayerId, string WorldName);

internal class CreateWorldCommandHandler(
    WorldGenerator worldGenerator,
    CreatureGenerator creatureGenerator,
    ICommandHandler<BootstrapWorldCommand, BootstrapWorldResult> bootstrapWorld
) : ICommandHandler<CreateWorldCommand, CreateWorldResult>
{
    public async Task<CreateWorldResult> Handle(
        CreateWorldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatureType = command.Race.ToCreatureType();
        var archetype = CreatureArchetype.For(command.PlayerClass.ToProfession());

        var worldResult = await worldGenerator.Generate(command.WorldInput, cancellationToken);

        var homeCountry = worldResult.Countries.First(c => c.DominantRace == creatureType);

        var startingCity = worldResult.Cities.First(c =>
            c.IsCapital && c.CountryId == homeCountry.Id
        );
        var startingDistrict = worldResult.Districts.First(d =>
            d.CityId == startingCity.Id && d.DistrictType == DistrictType.CityCenter
        );

        var birthYear = GameClock.EpochYear - command.Age;

        var playerResult = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: creatureType,
                Archetype: archetype,
                WorldId: worldResult.World.Id,
                BirthLocationId: startingDistrict.LocationId,
                MinLevel: 1,
                MaxLevel: 1,
                Name: command.Name,
                Gender: command.Gender.ToGender(),
                MinBirthYear: birthYear,
                MaxBirthYear: birthYear,
                StartingAttributeAllocation: command.StartingAttributeAllocation,
                PlayerClass: command.PlayerClass
            )
        );
        playerResult = creatureGenerator.AddStartingPotions(playerResult);
        playerResult.Creature.LocationId = startingDistrict.LocationId;

        var expeditionQuests = worldResult
            .DungeonExpeditions.Select(ExpeditionQuestGenerator.Generate)
            .ToArray();
        var quests = new QuestGeneratorResult(
            [
                .. expeditionQuests.SelectMany(result => result.Quests),
                .. worldResult.InitiationQuests,
            ],
            [
                .. expeditionQuests.SelectMany(result => result.Objectives),
                .. worldResult.InitiationQuestObjectives,
            ]
        );

        var monsterReputations = MonsterReputationSeeder.Seed(
            worldResult.World.Id,
            playerResult.Creature.Id,
            command.Race,
            command.PlayerClass,
            worldResult.Factions
        );

        // The city entrance is the one district every visit to a city is guaranteed to pass
        // through (it's the city's coarse-location anchor), so the seed check rides the same
        // lazy per-location catch-up every other sync command already uses without depending on
        // the player choosing to visit any particular district.
        var questSeedSchedules = worldResult
            .Districts.Where(district => district.DistrictType == DistrictType.CityEntrance)
            .Select(district => new QuestSeedSchedule
            {
                WorldId = worldResult.World.Id,
                LocationId = district.LocationId,
            })
            .ToArray();

        var bootstrapResult = await bootstrapWorld.Handle(
            new BootstrapWorldCommand
            {
                World = worldResult,
                Player = playerResult,
                Quests = quests,
                PlayerReputations = monsterReputations,
                QuestSeedSchedules = questSeedSchedules,
            },
            cancellationToken
        );

        return new CreateWorldResult(
            bootstrapResult.WorldId,
            bootstrapResult.PlayerId,
            worldResult.World.Name
        );
    }
}
