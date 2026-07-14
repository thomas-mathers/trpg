using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Commands;

namespace TRPG.Endpoints;

internal static class CheatEndpoints
{
    public static void MapCheatEndpoints(this WebApplication app)
    {
        app.MapPost("/sessions/{sessionId:guid}/cheat/grant-all-abilities", GrantAllAbilities);
    }

    private static async Task<IResult> GrantAllAbilities(
        Guid sessionId,
        GameSessionStateStore sessionStore,
        GrantAllAbilitiesCommandHandler grantAllAbilities,
        AbilityDefinitions abilityDefinitions,
        CancellationToken cancellationToken
    )
    {
        var state = sessionStore.Get(sessionId);
        if (state == null)
        {
            return Results.NotFound();
        }

        await state.TurnLock.WaitAsync(cancellationToken);
        try
        {
            await grantAllAbilities.Handle(
                new GrantAllAbilitiesCommand
                {
                    WorldId = state.Session.WorldId,
                    CreatureId = state.Session.PlayerId,
                    AbilityNames = abilityDefinitions.Abilities.Select(a => a.Name).ToArray(),
                },
                cancellationToken
            );

            if (state.Session.Combatants is { } combatants)
            {
                var player = combatants.Single(c => c.IsPlayer);
                player.CurrentAp = player.MaximumAp;
                player.CurrentMp = player.MaximumMp;
                foreach (var abilityName in player.CooldownRemainingByAbility.Keys.ToArray())
                {
                    player.CooldownRemainingByAbility[abilityName] = 0;
                }
            }
        }
        finally
        {
            state.TurnLock.Release();
        }

        return Results.NoContent();
    }
}
