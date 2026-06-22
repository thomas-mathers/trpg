using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class PersonService(TrpgDbContext context) {
    public async Task Add(Person person, CancellationToken cancellationToken = default) {
        context.Persons.Add(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Person?> GetById(Guid id, CancellationToken cancellationToken = default) {
        return await context.Persons.FindAsync([id], cancellationToken);
    }

    public async Task<ReadOnlyCollection<Person>> GetAllWithinRange(Guid worldId, Point center, float radius,
        CancellationToken cancellationToken = default) {
        var candidates = await context.Persons
            .Where(p => p.WorldId == worldId)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(p => Distance(p.Location.Coordinates, center) <= radius)
            .ToList().AsReadOnly();
    }

    public async Task Update(Person person, CancellationToken cancellationToken = default) {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default) {
        await context.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static float Distance(Point a, Point b) {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}