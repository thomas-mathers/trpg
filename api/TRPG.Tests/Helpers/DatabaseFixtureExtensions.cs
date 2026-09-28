using Microsoft.EntityFrameworkCore;
using TRPG.Domain.Models;

namespace TRPG.Tests.Helpers;

public static class DatabaseFixtureExtensions
{
    public static async Task<Creature> ReadCreature(this DatabaseFixture db, Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == creatureId,
                TestContext.Current.CancellationToken
            );
    }

    public static async Task<Guid?> ReadPropOccupantId(this DatabaseFixture db, Guid propId)
    {
        await using var verifyContext = db.CreateContext();
        var prop = await verifyContext
            .Props.AsNoTracking()
            .SingleAsync(p => p.Id == propId, TestContext.Current.CancellationToken);
        return prop switch
        {
            Seat seat => seat.OccupantId,
            Bed bed => bed.OccupantId,
            Workstation workstation => workstation.OccupantId,
            _ => throw new InvalidOperationException("Prop has no occupant."),
        };
    }
}
