using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Game;
using TRPG.Application.Game.Queries;

namespace TRPG.Endpoints;

internal static class CheatEndpoints
{
    public static void MapCheatEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions/{sessionId:guid}/cheat/grant-all-abilities", GrantAllAbilities);
    }

    private static async Task<IResult> GrantAllAbilities(
        Guid sessionId,
        GameSessionLocks sessionLocks,
        GetGameSessionQueryHandler getGameSession,
        GetCombatantsQueryHandler getCombatants,
        SetCombatantsCommandHandler setCombatants,
        GrantAllAbilitiesCommandHandler grantAllAbilities,
        AbilityDefinitions abilityDefinitions,
        CancellationToken cancellationToken
    )
    {
        await using var @lock = await sessionLocks.Acquire(sessionId, cancellationToken);
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { Lock = @lock },
            cancellationToken
        );

        await grantAllAbilities.Handle(
            new GrantAllAbilitiesCommand
            {
                WorldId = snapshot.WorldId,
                CreatureId = snapshot.PlayerId,
                AbilityNames = abilityDefinitions.Abilities.Select(a => a.Name).ToArray(),
            },
            cancellationToken
        );

        var combatants = await getCombatants.Handle(
            new GetCombatantsQuery { Lock = @lock },
            cancellationToken
        );
        if (combatants is { Count: > 0 })
        {
            var player = combatants.Single(c => c.IsPlayer);
            player.CurrentAp = player.MaximumAp;
            player.CurrentMp = player.MaximumMp;
            foreach (var abilityName in player.CooldownRemainingByAbility.Keys.ToArray())
            {
                player.CooldownRemainingByAbility[abilityName] = 0;
            }

            await setCombatants.Handle(
                new SetCombatantsCommand { Lock = @lock, Combatants = combatants },
                cancellationToken
            );
        }

        return Results.NoContent();
    }
}
