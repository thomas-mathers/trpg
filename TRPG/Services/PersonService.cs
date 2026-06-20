using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

public class PersonService(TrpgDbContext context)
{
    public async Task Add(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Add(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Person?> GetById(Guid id, CancellationToken cancellationToken = default)
        => await context.Persons.FindAsync([id], cancellationToken);

    public async Task<ReadOnlyCollection<Person>> GetAllWithinRange(Location center, float radius, CancellationToken cancellationToken = default)
    {
        var candidates = await context.Persons
            .Where(p => p.Location.WorldId == center.WorldId)
            .ToListAsync(cancellationToken);

        return candidates
            .Where(p => Distance(p.Location.Coordinates, center.Coordinates) <= radius)
            .ToList().AsReadOnly();
    }

    public async Task Update(Person person, CancellationToken cancellationToken = default)
    {
        context.Persons.Update(person);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task Delete(Guid id, CancellationToken cancellationToken = default)
        => await context.Persons.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);

    private static float Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
