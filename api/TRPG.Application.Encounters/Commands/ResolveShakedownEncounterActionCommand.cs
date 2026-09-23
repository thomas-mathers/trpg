using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Mappers;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveShakedownEncounterActionCommand : IEncounterResolutionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required ShakedownEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
}

internal class ResolveShakedownEncounterActionCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<RemoveGoldCommand> removeGold,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<StartFightCommand> startFight,
    EncounterDepartureResolver encounterDepartureResolver,
    EncounterFleeResolver encounterFleeResolver,
    IOptionsSnapshot<FleeOptions> fleeOptions,
    IOptionsSnapshot<IntimidationOptions> intimidationOptions
)
    : EncounterResolutionCommandHandlerBase<
        ShakedownEncounter,
        ResolveShakedownEncounterActionCommand,
        ShakedownEncounterResolutionFact
    >(context)
{
    protected override async Task<ShakedownEncounterResolutionFact> Resolve(
        ResolveShakedownEncounterActionCommand command,
        ShakedownEncounter encounter,
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

        var members = memberIds.Select(id => membersById[id]).ToArray();
        var resolutionContext = new ShakedownEncounterResolutionContext(
            fleeOptions.Value,
            intimidationOptions.Value,
            player.ToEvadeParticipant(),
            members.Select(member => member.ToEvadeParticipant()).ToArray(),
            player.ToIntimidationParticipant(),
            members.Select(member => member.ToIntimidationParticipant()).ToArray()
        );

        var outcome = ShakedownEncounterActionResolver.Resolve(
            command.Action,
            resolutionContext,
            Random.Shared.NextDouble()
        );

        await ApplyEncounterOutcome(outcome, command, encounter, player, cancellationToken);

        return new ShakedownEncounterResolutionFact(
            EncounterId: command.EncounterId,
            Outcome: outcome,
            FactionName: encounter.FactionName,
            LocationName: encounter.LocationName!,
            TollAmount: encounter.TollAmount,
            MemberNames: encounter.Members.Select(member => member.Name).ToArray()
        );
    }

    private async Task ApplyEncounterOutcome(
        ShakedownEncounterResolutionOutcome outcome,
        ResolveShakedownEncounterActionCommand command,
        ShakedownEncounter encounter,
        Creature player,
        CancellationToken cancellationToken
    )
    {
        if (outcome == ShakedownEncounterResolutionOutcome.PaidToll)
        {
            await removeGold.Handle(
                new RemoveGoldCommand
                {
                    Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                    Amount = encounter.TollAmount,
                },
                cancellationToken
            );

            if (encounter.DepartureDestinationLocationId != null)
            {
                var playtime = await getPlaytime.Handle(
                    new GetPlaytimeQuery { SessionId = command.SessionId },
                    cancellationToken
                );
                await encounterDepartureResolver.TryResume(
                    encounter,
                    player,
                    playtime,
                    cancellationToken
                );
            }
            return;
        }

        if (outcome == ShakedownEncounterResolutionOutcome.Fled)
        {
            var playtime = await getPlaytime.Handle(
                new GetPlaytimeQuery { SessionId = command.SessionId },
                cancellationToken
            );
            await encounterFleeResolver.Resolve(encounter, player, playtime, cancellationToken);
            return;
        }

        var startsFight = outcome switch
        {
            ShakedownEncounterResolutionOutcome.IntimidateFailed
            or ShakedownEncounterResolutionOutcome.Fought
            or ShakedownEncounterResolutionOutcome.FleeFailed => true,
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
            },
            cancellationToken
        );
    }
}
