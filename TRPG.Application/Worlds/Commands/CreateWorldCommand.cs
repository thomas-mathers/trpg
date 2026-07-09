using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Commands;

public class CreateWorldCommand
{
    public required WorldGeneratorInput WorldInput { get; init; }
    public required CreatureType PlayerCreatureType { get; init; }
    public required Profession PlayerProfession { get; init; }
    public required string PlayerName { get; init; }
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
        var worldResult = await worldGenerator.Generate(command.WorldInput, cancellationToken);

        var homeCountry = worldResult.Countries.First(c =>
            c.DominantRace == command.PlayerCreatureType
        );
        var startingCity = worldResult.Cities.First(c =>
            c.IsCapital && c.CountryId == homeCountry.Id
        );
        var startingState = worldResult.States.First(s => s.Id == startingCity.StateId);
        var startingDistrict = worldResult.Districts.First(d =>
            d.CityId == startingCity.Id && d.DistrictType == DistrictType.CityCenter
        );

        var playerResult = creatureGenerator.Generate(
            new CreatureGeneratorInput(
                CreatureType: command.PlayerCreatureType,
                Profession: command.PlayerProfession,
                WorldId: worldResult.World.Id,
                BirthStateId: startingState.Id,
                StateId: startingState.Id,
                Level: 1,
                Name: command.PlayerName
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
