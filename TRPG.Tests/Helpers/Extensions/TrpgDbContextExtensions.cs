using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Tests.Helpers.Extensions;

internal static class TrpgDbContextExtensions
{
    public static async Task<Creature> AddCreature(
        this TrpgDbContext context,
        Creature creature,
        CancellationToken cancellationToken
    )
    {
        context.Creatures.Add(creature);
        await context.SaveChangesAsync(cancellationToken);
        return creature;
    }

    public static async Task<IReadOnlyList<Creature>> AddCreature(
        this TrpgDbContext context,
        IEnumerable<Creature> creatures,
        CancellationToken cancellationToken
    )
    {
        var list = creatures.ToArray();
        context.Creatures.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<Building> AddBuilding(
        this TrpgDbContext context,
        Building building,
        CancellationToken cancellationToken
    )
    {
        context.Buildings.Add(building);
        await context.SaveChangesAsync(cancellationToken);
        return building;
    }

    public static async Task<IReadOnlyList<Building>> AddBuilding(
        this TrpgDbContext context,
        IEnumerable<Building> buildings,
        CancellationToken cancellationToken
    )
    {
        var list = buildings.ToArray();
        context.Buildings.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<Room> AddRoom(
        this TrpgDbContext context,
        Room room,
        CancellationToken cancellationToken
    )
    {
        context.Rooms.Add(room);
        await context.SaveChangesAsync(cancellationToken);
        return room;
    }

    public static async Task<IReadOnlyList<Room>> AddRoom(
        this TrpgDbContext context,
        IEnumerable<Room> rooms,
        CancellationToken cancellationToken
    )
    {
        var list = rooms.ToArray();
        context.Rooms.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<Faction> AddFaction(
        this TrpgDbContext context,
        Faction faction,
        CancellationToken cancellationToken
    )
    {
        context.Factions.Add(faction);
        await context.SaveChangesAsync(cancellationToken);
        return faction;
    }

    public static async Task<IReadOnlyList<Faction>> AddFaction(
        this TrpgDbContext context,
        IEnumerable<Faction> factions,
        CancellationToken cancellationToken
    )
    {
        var list = factions.ToArray();
        context.Factions.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<World> AddWorld(
        this TrpgDbContext context,
        World world,
        CancellationToken cancellationToken
    )
    {
        context.Worlds.Add(world);
        await context.SaveChangesAsync(cancellationToken);
        return world;
    }

    public static async Task<IReadOnlyList<World>> AddWorld(
        this TrpgDbContext context,
        IEnumerable<World> worlds,
        CancellationToken cancellationToken
    )
    {
        var list = worlds.ToArray();
        context.Worlds.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<Item> AddItem(
        this TrpgDbContext context,
        Item item,
        CancellationToken cancellationToken
    )
    {
        context.Items.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return item;
    }

    public static async Task<IReadOnlyList<Item>> AddItem(
        this TrpgDbContext context,
        IEnumerable<Item> items,
        CancellationToken cancellationToken
    )
    {
        var list = items.ToArray();
        context.Items.AddRange(list);
        await context.SaveChangesAsync(cancellationToken);
        return list;
    }

    public static async Task<Country> AddCountry(
        this TrpgDbContext context,
        Country country,
        CancellationToken cancellationToken
    )
    {
        context.Countries.Add(country);
        await context.SaveChangesAsync(cancellationToken);
        return country;
    }

    public static async Task<State> AddState(
        this TrpgDbContext context,
        State state,
        CancellationToken cancellationToken
    )
    {
        context.States.Add(state);
        await context.SaveChangesAsync(cancellationToken);
        return state;
    }

    public static async Task<GameSession> AddGameSession(
        this TrpgDbContext context,
        GameSession session,
        CancellationToken cancellationToken
    )
    {
        context.GameSessions.Add(session);
        await context.SaveChangesAsync(cancellationToken);
        return session;
    }

    public static async Task<Fight> AddFight(
        this TrpgDbContext context,
        Fight fight,
        CancellationToken cancellationToken
    )
    {
        context.Fights.Add(fight);
        await context.SaveChangesAsync(cancellationToken);
        return fight;
    }
}
