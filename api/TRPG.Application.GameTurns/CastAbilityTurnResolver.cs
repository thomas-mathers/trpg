using TRPG.Application.Abilities;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class CastAbilityTurnResolver(
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    ICommandHandler<CastAbilityCommand, CastAbilityResult> castAbility,
    ICommandHandler<OpenFightWithAbilityCommand, PlayerCombatActionResult> openFightWithAbility,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public async Task<ActionOutcome> Resolve(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = session.PlayerId },
            cancellationToken
        );
        var ability = abilities.FirstOrDefault(a => a.Name == abilityName);
        if (ability is null)
        {
            return ActionOutcome.Failed(ActionFailure.AbilityNotFound);
        }

        try
        {
            await (
                ability is AttackAbility
                    ? OpenFight(session, targetId, abilityName, gameTime, cancellationToken)
                    : CastSupport(session, targetId, abilityName, gameTime, cancellationToken)
            );
        }
        catch (InvalidOperationException)
        {
            return ActionOutcome.Failed(ActionFailure.AbilityUnavailable);
        }

        await RefreshScene(session, gameTime, cancellationToken);

        return ActionOutcome.Success;
    }

    private async Task CastSupport(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await castAbility.Handle(
            new CastAbilityCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                TargetId = targetId,
                AbilityName = abilityName,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private async Task OpenFight(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await openFightWithAbility.Handle(
            new OpenFightWithAbilityCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                TargetId = targetId,
                AbilityName = abilityName,
                GameTime = gameTime,
            },
            cancellationToken
        );

    private async Task RefreshScene(
        GameTurnSession session,
        GameInstant gameTime,
        CancellationToken cancellationToken
    ) =>
        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );
}
