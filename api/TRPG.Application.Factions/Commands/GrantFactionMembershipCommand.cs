using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Commands;

public record GrantFactionMembershipCommand(Guid WorldId, Guid CreatureId, Guid FactionId);

internal class GrantFactionMembershipCommandHandler(IFactionsDbContext context)
    : ICommandHandler<GrantFactionMembershipCommand>
{
    public async Task Handle(
        GrantFactionMembershipCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO faction_members (id, world_id, creature_id, faction_id, role)
            VALUES ({Guid.NewGuid()}, {command.WorldId}, {command.CreatureId}, {command.FactionId}, {FactionRole.Member.ToString()})
            ON CONFLICT (creature_id, faction_id) DO NOTHING
            """,
            cancellationToken
        );
}
