using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Models;

namespace TRPG.Services;

internal class ReputationService(TrpgDbContext context) {
    public async Task AdjustReputation(Guid personId, Guid factionId, int deltaScore,
        CancellationToken cancellationToken = default) {
        var reputation = await context.Reputations
            .FirstOrDefaultAsync(r => r.PersonId == personId && r.FactionId == factionId, cancellationToken);

        if (reputation == null) {
            context.Reputations.Add(new Reputation {
                Id = Guid.NewGuid(),
                PersonId = personId,
                FactionId = factionId,
                Score = deltaScore
            });
        }
        else {
            reputation.Score += deltaScore;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Reputation>> GetAllByPersonId(Guid personId,
        CancellationToken cancellationToken = default) {
        var list = await context.Reputations
            .Where(r => r.PersonId == personId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}