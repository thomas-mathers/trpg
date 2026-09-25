using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IRoutingDbContext : ITrpgDbContext
{
    DbSet<CreatureRouteSchedule> CreatureRouteSchedules { get; }
    DbSet<Route> Routes { get; }
    DbSet<RouteStep> RouteSteps { get; }
    DbSet<RouteTraveler> RouteTravelers { get; }
    DbSet<RouteTravelerMember> RouteTravelerMembers { get; }
    DbSet<LocationConnector> LocationConnectors { get; }
    DbSet<TravelConnector> TravelConnectors { get; }
}
