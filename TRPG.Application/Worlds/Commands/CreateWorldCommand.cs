using TRPG.Application.Worlds.Generators;
using TRPG.Contracts;
using TRPG.Data.Models;
using Profession = TRPG.Contracts.Profession;
using DomainProfession = TRPG.Data.Models.Profession;

namespace TRPG.Application.Worlds.Commands;

public class CreateWorldCommand
{
    public required WorldGeneratorInput WorldInput { get; init; }
    public required Race PlayerRace { get; init; }
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
        var playerCreatureType = ToCreatureType(command.PlayerRace);
        var playerProfession = ToDomainProfession(command.PlayerProfession);

        var worldResult = await worldGenerator.Generate(command.WorldInput, cancellationToken);

        var homeCountry = worldResult.Countries.First(c =>
            c.DominantRace == playerCreatureType
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
                CreatureType: playerCreatureType,
                Profession: playerProfession,
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

    private static CreatureType ToCreatureType(Race race) =>
        race switch
        {
            Race.Human => CreatureType.Human,
            Race.Elf => CreatureType.Elf,
            Race.Dwarf => CreatureType.Dwarf,
            Race.Orc => CreatureType.Orc,
            Race.Halfling => CreatureType.Halfling,
            Race.Gnome => CreatureType.Gnome,
            _ => throw new ArgumentOutOfRangeException(nameof(race), race, null),
        };

    private static DomainProfession ToDomainProfession(Profession profession) =>
        profession switch
        {
            Profession.Knight => DomainProfession.Knight,
            Profession.Rogue => DomainProfession.Rogue,
            Profession.Ranger => DomainProfession.Ranger,
            Profession.Mage => DomainProfession.Mage,
            Profession.Cleric => DomainProfession.Cleric,
            Profession.Mercenary => DomainProfession.Mercenary,
            Profession.Alchemist => DomainProfession.Alchemist,
            Profession.Blacksmith => DomainProfession.Blacksmith,
            Profession.Scholar => DomainProfession.Scholar,
            Profession.Merchant => DomainProfession.Merchant,
            Profession.Politician => DomainProfession.Politician,
            Profession.StableMaster => DomainProfession.StableMaster,
            Profession.Bartender => DomainProfession.Bartender,
            Profession.Guard => DomainProfession.Guard,
            _ => throw new ArgumentOutOfRangeException(nameof(profession), profession, null),
        };
}
