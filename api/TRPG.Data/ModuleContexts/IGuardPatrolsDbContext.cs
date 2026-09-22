using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IGuardPatrolsDbContext : ITrpgDbContext
{
    DbSet<GuardPatrolMember> GuardPatrolMembers { get; }
}
