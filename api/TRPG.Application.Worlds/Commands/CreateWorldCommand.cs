using TRPG.Application.Common.Handling;
using TRPG.Application.Worlds;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Mappers;
using TRPG.Data.Models;

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
    QuestGenerator questGenerator,
    BootstrapWorldCommandHandler bootstrapWorld
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
        var startingState = worldResult.States.First(s => s.Id == startingCity.StateId);
        var startingDistrict = worldResult.Districts.First(d =>
            d.CityId == startingCity.Id && d.DistrictType == DistrictType.CityCenter
        );

        var birthYear = WorldEpoch.Year - command.Age;

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
                StartingAttributeAllocation: command.StartingAttributeAllocation
            )
        );
        playerResult = creatureGenerator.AddStartingPotions(playerResult);
        playerResult.Creature.LocationId = startingDistrict.LocationId;

        var quests = questGenerator.Generate(worldResult, startingState.Id);

        var bootstrapResult = await bootstrapWorld.Handle(
            worldResult,
            playerResult,
            quests,
            cancellationToken
        );

        return new CreateWorldResult(
            bootstrapResult.WorldId,
            bootstrapResult.PlayerId,
            worldResult.World.Name
        );
    }
}
