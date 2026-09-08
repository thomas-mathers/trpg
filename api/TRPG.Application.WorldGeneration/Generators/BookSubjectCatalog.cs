using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

// Book subjects come from the world that was actually generated, so a library shelves histories of
// countries the player can walk to rather than of invented places.
public static class BookSubjectCatalog
{
    private static readonly Profession[] WrittenTrades =
    [
        Profession.Blacksmith,
        Profession.Alchemist,
        Profession.Jeweler,
        Profession.Carpenter,
        Profession.Tailor,
        Profession.Baker,
        Profession.Scholar,
        Profession.Cleric,
    ];

    private static readonly CreatureType[] StudiedCreatures =
    [
        CreatureType.Dragon,
        CreatureType.Undead,
        CreatureType.Giant,
        CreatureType.Elf,
        CreatureType.Dwarf,
        CreatureType.Gnome,
    ];

    public static IReadOnlyCollection<BookSubject> From(
        IReadOnlyCollection<Country> countries,
        IReadOnlyCollection<State> states,
        IReadOnlyCollection<City> cities,
        IReadOnlyCollection<Faction> factions
    ) =>
        [
            .. countries.Select(country => new BookSubject(BookSubjectType.Country, country.Name)),
            .. states.Select(state => new BookSubject(BookSubjectType.State, state.Name)),
            .. cities.Select(city => new BookSubject(BookSubjectType.City, city.Name)),
            .. factions.Select(faction => new BookSubject(BookSubjectType.Faction, faction.Name)),
            .. WrittenTrades.Select(trade => new BookSubject(
                BookSubjectType.Profession,
                trade.ToString()
            )),
            .. StudiedCreatures.Select(creature => new BookSubject(
                BookSubjectType.CreatureType,
                creature.ToString()
            )),
        ];
}
