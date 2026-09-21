using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICaravansDbContext : ITrpgDbContext
{
    DbSet<CaravanRoute> CaravanRoutes { get; }
    DbSet<CaravanRouteStop> CaravanRouteStops { get; }
    DbSet<Caravan> Caravans { get; }
    DbSet<CaravanTicket> CaravanTickets { get; }
}
