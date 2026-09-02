using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IWorldsDbContext : ITrpgDbContext
{
    DbSet<BuildingOwner> BuildingOwners { get; }
    DbSet<Building> Buildings { get; }
    DbSet<City> Cities { get; }
    DbSet<Country> Countries { get; }
    DbSet<District> Districts { get; }
    DbSet<DoorConnectorKey> DoorConnectorKeys { get; }
    DbSet<DoorConnector> DoorConnectors { get; }
    DbSet<LocationConnector> LocationConnectors { get; }
    DbSet<Location> Locations { get; }
    DbSet<Room> Rooms { get; }
    DbSet<State> States { get; }
    DbSet<TravelConnector> TravelConnectors { get; }
    DbSet<World> Worlds { get; }
}
