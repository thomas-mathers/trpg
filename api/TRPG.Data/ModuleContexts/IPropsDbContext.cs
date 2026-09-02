using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IPropsDbContext : ITrpgDbContext
{
    DbSet<Prop> Props { get; }
}
