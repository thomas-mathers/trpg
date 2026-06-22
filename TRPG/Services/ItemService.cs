using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class ItemService(TrpgDbContext context) {
    public async Task Add(Item item, CancellationToken cancellationToken = default) {
        context.Items.Add(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Item?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Items.FindAsync([id], cancellationToken);
    }

    public async Task Update(Item item, CancellationToken cancellationToken = default) {
        context.Items.Update(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Items.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken);
    }
}