using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Data.ModuleContexts;

public interface IBooksDbContext : ITrpgDbContext
{
    DbSet<BookWork> BookWorks { get; }
    DbSet<BookPage> BookPages { get; }
    DbSet<Secret> Secrets { get; }
    DbSet<Item> Items { get; }
}
