using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

internal sealed class EncounterEngagementManager(
    IEncountersDbContext context,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<EngageCreaturesCommand> engageCreatures,
    ICommandHandler<ReleaseCreaturesCommand> releaseCreatures
)
{
    public async Task Engage(
        Encounter encounter,
        GameInstant gameTime,
        CancellationToken cancellationToken = default
    )
    {
        var participantIds = GetParticipantIds(encounter);
        var creatures = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = participantIds },
            cancellationToken
        );
        var availableIds = participantIds
            .Where(id => creatures.TryGetValue(id, out var creature) && !creature.IsEngaged)
            .ToArray();
        await engageCreatures.Handle(
            new EngageCreaturesCommand
            {
                WorldId = encounter.WorldId,
                CreatureIds = availableIds,
                GameTime = gameTime,
            },
            cancellationToken
        );
    }

    public async Task ReconcileResolved(
        Encounter resolvedEncounter,
        GameInstant gameTime,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounters = await context
            .Encounters.AsNoTracking()
            .Where(encounter =>
                encounter.WorldId == resolvedEncounter.WorldId
                && encounter.PlayerId == resolvedEncounter.PlayerId
                && encounter.State == EncounterState.Active
            )
            .ToArrayAsync(cancellationToken);
        var activeParticipantIds = activeEncounters.SelectMany(GetParticipantIds).ToHashSet();
        var releasedIds = GetParticipantIds(resolvedEncounter)
            .Where(id => !activeParticipantIds.Contains(id))
            .ToArray();
        await releaseCreatures.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = resolvedEncounter.WorldId,
                CreatureIds = releasedIds,
                GameTime = gameTime,
            },
            cancellationToken
        );
        foreach (var activeEncounter in activeEncounters)
        {
            await Engage(activeEncounter, gameTime, cancellationToken);
        }
    }

    internal static IReadOnlyCollection<Guid> GetParticipantIds(Encounter encounter) =>
        encounter switch
        {
            FightEncounter fight => [encounter.PlayerId, .. fight.CombatantIds],
            HostileEncounter hostile =>
            [
                encounter.PlayerId,
                .. hostile.Members.Select(member => member.Id),
            ],
            ShakedownEncounter shakedown =>
            [
                encounter.PlayerId,
                .. shakedown.Members.Select(member => member.Id),
            ],
            GuardEncounter guard => [encounter.PlayerId, guard.GuardCreatureId],
            SuspicionEncounter suspicion => [encounter.PlayerId, suspicion.GuardCreatureId],
            TheftEncounter theft => [encounter.PlayerId, theft.ConfrontingCreatureId],
            TrapEncounter => [encounter.PlayerId],
            _ => [encounter.PlayerId],
        };
}
