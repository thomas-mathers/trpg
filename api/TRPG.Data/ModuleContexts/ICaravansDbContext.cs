using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICaravansDbContext : ITrpgDbContext
{
    DbSet<CaravanFare> CaravanFares { get; }
    DbSet<CaravanTicket> CaravanTickets { get; }
}
