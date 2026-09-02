using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IRoomBookingsDbContext : ITrpgDbContext
{
    DbSet<RoomBooking> RoomBookings { get; }
}
