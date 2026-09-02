using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface ICrimesDbContext : ITrpgDbContext
{
    DbSet<Crime> Crimes { get; }
    DbSet<CrimeWitness> CrimeWitnesses { get; }
}
