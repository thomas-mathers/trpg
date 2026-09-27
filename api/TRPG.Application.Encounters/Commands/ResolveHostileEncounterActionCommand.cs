using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Mappers;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveHostileEncounterActionCommand : IEncounterResolutionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required HostileEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public GameInstant GameTime { get; init; } = GameClock.Epoch;
}

internal class ResolveHostileEncounterActionCommandHandler(
    IEncountersDbContext context,
    EncounterEngagementManager engagementManager,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight,
    EncounterFleeResolver encounterFleeResolver,
    IOptionsSnapshot<FleeOptions> fleeOptions
)
    : EncounterResolutionCommandHandlerBase<
        HostileEncounter,
        ResolveHostileEncounterActionCommand,
        HostileEncounterResolutionFact
    >(context, engagementManager)
{
    protected override async Task<HostileEncounterResolutionFact> Resolve(
        ResolveHostileEncounterActionCommand command,
        HostileEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        var memberIds = encounter.Members.Select(member => member.Id).ToArray();
        var membersById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = memberIds },
            cancellationToken
        );
        var missingMemberIds = memberIds.Where(id => !membersById.ContainsKey(id)).ToArray();
        if (missingMemberIds.Length > 0)
        {
            throw new EntityNotFoundException(nameof(Creature), missingMemberIds[0]);
        }

        var outcome = HostileEncounterActionResolver.Resolve(
            command.Action,
            fleeOptions.Value,
            player.ToEvadeParticipant(),
            memberIds.Select(id => membersById[id].ToEvadeParticipant()).ToArray(),
            Random.Shared.NextDouble()
        );

        await ApplyEncounterOutcome(outcome, command, encounter, player, cancellationToken);

        return new HostileEncounterResolutionFact(
            EncounterId: command.EncounterId,
            Outcome: outcome,
            FactionName: encounter.FactionName,
            LocationName: encounter.LocationName!,
            MemberNames: encounter.Members.Select(member => member.Name).ToArray()
        );
    }

    private async Task ApplyEncounterOutcome(
        HostileEncounterResolutionOutcome outcome,
        ResolveHostileEncounterActionCommand command,
        HostileEncounter encounter,
        Creature player,
        CancellationToken cancellationToken
    )
    {
        if (outcome == HostileEncounterResolutionOutcome.Fled)
        {
            await encounterFleeResolver.Resolve(
                encounter,
                player,
                command.GameTime,
                cancellationToken
            );
            return;
        }

        var startsFight = outcome switch
        {
            HostileEncounterResolutionOutcome.FleeFailed
            or HostileEncounterResolutionOutcome.Attacked => true,
            _ => false,
        };
        if (!startsFight)
        {
            return;
        }

        var enemyCreatureIds = encounter.Members.Select(member => member.Id).ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = enemyCreatureIds,
                State = CreatureState.Alerted,
            },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = enemyCreatureIds,
                HasSurpriseRound = false,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
