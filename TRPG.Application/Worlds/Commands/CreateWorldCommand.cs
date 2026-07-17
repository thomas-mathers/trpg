using TRPG.Application.GameSessions;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Mappers;
using TRPG.Contracts.Worlds.Requests;
using TRPG.Data.Models;
using Gender = TRPG.Contracts.Worlds.Requests.Gender;

namespace TRPG.Application.Worlds.Commands;

public class CreateWorldCommand
{
    public required WorldGeneratorInput WorldInput { get; init; }
    public required string Name { get; init; }
    public required Gender Gender { get; init; }
    public required Age Age { get; init; }
    public required Race Race { get; init; }
    public required PlayerClass PlayerClass { get; init; }
}

public record CreateWorldResult(Guid WorldId, Guid PlayerId, string WorldName);

public class CreateWorldCommandHandler(
    WorldGenerator worldGenerator,
    CreatureGenerator creatureGenerator,
    BootstrapWorldCommandHandler bootstrapWorld
)
{
    public async Task<CreateWorldResult> Handle(
        CreateWorldCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creatureType = command.Race.ToCreatureType();
        var profession = command.PlayerClass.ToProfession();

        var worldResult = await worldGenerator.Generate(command.WorldInput, cancellationToken);

        var homeCountry = worldResult.Countries.First(c => c.DominantRace == creatureType);

        var startingCity = worldResult.Cities.First(c =>
            c.IsCapital && c.CountryId == homeCountry.Id
        );
        var startingState = worldResult.States.First(s => s.Id == startingCity.StateId);
        var startingDistrict = worldResult.Districts.First(d =>
            d.CityId == startingCity.Id && d.DistrictType == DistrictType.CityCenter
        );

        var birthYear = GameClock.EpochYear - (int)command.Age;

        var playerResult = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: creatureType,
                Profession: profession,
                WorldId: worldResult.World.Id,
                BirthStateId: startingState.Id,
                StateId: startingState.Id,
                Level: 1,
                Name: command.Name,
                Gender: command.Gender.ToGender(),
                MinBirthYear: birthYear,
                MaxBirthYear: birthYear
            )
        );
        playerResult.Creature.CityId = startingCity.Id;
        playerResult.Creature.DistrictId = startingDistrict.Id;

        var bootstrapResult = await bootstrapWorld.Handle(
            worldResult,
            playerResult,
            cancellationToken
        );

        return new CreateWorldResult(
            bootstrapResult.WorldId,
            bootstrapResult.PlayerId,
            worldResult.World.Name
        );
    }
}
