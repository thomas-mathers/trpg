using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Mappers;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions;
using TRPG.Contracts.Combat.Requests;
using TRPG.Contracts.Combat.Responses;

namespace TRPG.Application.GameTurns;

internal class ResolveCombatActionHandler(
    ApplyPassiveRegenCommandHandler applyPassiveRegen,
    GetActiveFightCombatantsQueryHandler getCombatants,
    CombatEngine combatEngine,
    ResolveCombatRoundCommandHandler resolveCombatRound
)
{
    public async Task<CombatActionResponse> Handle(
        GameSessionIdentity session,
        PlayerCombatAction action,
        CancellationToken cancellationToken = default
    )
    {
        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = session.SessionId,
                CreatureIds = [session.PlayerId],
            },
            cancellationToken
        );

        var combatants = await getCombatants.Handle(
            new GetActiveFightCombatantsQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (combatants.Count == 0)
        {
            return CombatActionResponse.Rejected("There's no fight to act in right now.");
        }

        var resolverResult = new PlayerCombatActionResolver(combatants).Resolve(action);

        if (resolverResult.ErrorMessage is not null)
        {
            return CombatActionResponse.Rejected(resolverResult.ErrorMessage);
        }

        var state = combatEngine.ProcessRound(combatants, resolverResult.Result!);

        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Combatants = combatants,
                State = state,
                PublishEvents = false,
            },
            cancellationToken
        );

        return new CombatActionResponse(
            new CombatUpdatePayload(
                FightStateMapper.ToFightState(combatants),
                CombatRoundEventMapper.ToCombatRoundEvents(state.Events)
            ),
            null,
            state.Outcome == Data.Models.CombatOutcome.Ongoing ? null : state.Outcome.ToContract()
        );
    }
}
