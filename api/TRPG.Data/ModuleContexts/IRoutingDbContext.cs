using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IRoutingDbContext : ITrpgDbContext
{
    DbSet<Route> Routes { get; }
    DbSet<RouteStop> RouteStops { get; }
    DbSet<RouteTraveler> RouteTravelers { get; }
    DbSet<RouteTravelerMember> RouteTravelerMembers { get; }
}
