using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Worlds.Encounters.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Encounters.Requests;
using TRPG.Contracts.Encounters.Responses;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Encounters.Commands;

internal class ResolveEncounterActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required PlayerEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public required Guid EncounterGroupId { get; init; }
    public required Guid EncounterLocationId { get; init; }
    public Guid? ArrivalOriginLocationId { get; init; }
}

internal record EncounterActionResolution(
    HostileEncounterActionKind ActionKind,
    EncounterResolutionFact Fact,
    FightState? FightState,
    SceneResult? UpdatedScene
);

internal class ResolveEncounterActionCommandHandler(
    GetEncounterGroupContextQueryHandler getEncounterGroupContext,
    GetCreatureByIdQueryHandler getCreatureById,
    CompleteEncounterCommandHandler completeEncounter,
    UpdateCreaturesCommandHandler updateCreatures,
    GetSceneWithCatchUpQueryHandler getSceneWithCatchUp,
    StartFightCommandHandler startFight,
    GetLocationByIdQueryHandler getLocationById
)
{
    public async Task<EncounterActionResolution> Handle(
        ResolveEncounterActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var groupContext = await getEncounterGroupContext.Handle(
            new GetEncounterGroupContextQuery { EncounterGroupId = command.EncounterGroupId },
            cancellationToken
        );
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var groupPower = groupContext.LivingMembers.Sum(member => member.Level);
        var actionKind = ToActionKind(command.Action);
        var outcome = HostileEncounterActionResolver.Resolve(
            actionKind,
            player!.Level,
            groupPower,
            Random.Shared.Next(100)
        );

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        var effects = await ApplyEncounterOutcome(
            outcome,
            command,
            player,
            groupContext,
            cancellationToken
        );

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.EncounterLocationId },
            cancellationToken
        );

        var fact = new EncounterResolutionFact(
            EncounterId: command.EncounterId,
            Outcome: ToResolutionOutcome(outcome),
            FactionName: groupContext.Faction.Name,
            LocationName: location!.Name,
            MemberNames: groupContext.LivingMembers.Select(member => member.Name).ToArray()
        );

        return new EncounterActionResolution(
            actionKind,
            fact,
            effects.FightState,
            effects.UpdatedScene
        );
    }

    private async Task<EncounterOutcomeEffects> ApplyEncounterOutcome(
        HostileEncounterActionOutcome outcome,
        ResolveEncounterActionCommand command,
        Creature player,
        EncounterGroupContext groupContext,
        CancellationToken cancellationToken
    )
    {
        if (
            outcome == HostileEncounterActionOutcome.Retreated
            && command.ArrivalOriginLocationId is { } originLocationId
        )
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = [player.Id],
                    LocationId = originLocationId,
                },
                cancellationToken
            );
            var updatedScene = await getSceneWithCatchUp.Handle(
                new GetSceneWithCatchUpQuery
                {
                    WorldId = command.WorldId,
                    PlayerId = command.PlayerId,
                    SessionId = command.SessionId,
                },
                cancellationToken
            );
            return new EncounterOutcomeEffects(null, updatedScene);
        }

        var startsFight = outcome switch
        {
            HostileEncounterActionOutcome.EvadeFailed
            or HostileEncounterActionOutcome.RetreatFailed
            or HostileEncounterActionOutcome.Attacked => true,
            _ => false,
        };
        if (!startsFight)
        {
            return new EncounterOutcomeEffects(null, null);
        }

        var combatants = await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = groupContext.LivingMembers.Select(member => member.Id).ToArray(),
                EncounterId = command.EncounterId,
            },
            cancellationToken
        );

        return new EncounterOutcomeEffects(FightStateMapper.ToFightState(combatants), null);
    }

    private static HostileEncounterActionKind ToActionKind(PlayerEncounterAction action) =>
        action switch
        {
            AttackEncounterAction => HostileEncounterActionKind.Attack,
            EvadeEncounterAction => HostileEncounterActionKind.Evade,
            RetreatEncounterAction => HostileEncounterActionKind.Retreat,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static EncounterResolutionOutcome ToResolutionOutcome(
        HostileEncounterActionOutcome outcome
    ) =>
        outcome switch
        {
            HostileEncounterActionOutcome.Attacked => EncounterResolutionOutcome.Attacked,
            HostileEncounterActionOutcome.Evaded => EncounterResolutionOutcome.Evaded,
            HostileEncounterActionOutcome.EvadeFailed => EncounterResolutionOutcome.EvadeFailed,
            HostileEncounterActionOutcome.Retreated => EncounterResolutionOutcome.Retreated,
            HostileEncounterActionOutcome.RetreatFailed => EncounterResolutionOutcome.RetreatFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}

internal record EncounterOutcomeEffects(FightState? FightState, SceneResult? UpdatedScene);
