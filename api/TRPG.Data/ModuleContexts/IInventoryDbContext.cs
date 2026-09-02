using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IInventoryDbContext : ITrpgDbContext
{
    DbSet<Item> Items { get; }
}
