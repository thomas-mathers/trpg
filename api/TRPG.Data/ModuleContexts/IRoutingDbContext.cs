using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IRoutingDbContext : ITrpgDbContext
{
    DbSet<TravelCircuit> TravelCircuits { get; }
    DbSet<TravelCircuitLeg> TravelCircuitLegs { get; }
    DbSet<Journey> Journeys { get; }
    DbSet<JourneyLeg> JourneyLegs { get; }
    DbSet<JourneyMember> JourneyMembers { get; }
}
