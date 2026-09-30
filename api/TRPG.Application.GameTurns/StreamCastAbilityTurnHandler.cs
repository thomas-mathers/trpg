using System.Text.Json;
using TRPG.Application.Abilities;
using TRPG.Application.Combat;
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

internal record AbilityCastFact(
    string AbilityName,
    string AbilityDescription,
    string TargetName,
    bool TargetIsPlayer
);

internal class StreamCastAbilityTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
    ICommandHandler<CastAbilityCommand, CastAbilityResult> castAbility,
    ICommandHandler<OpenFightWithAbilityCommand, PlayerCombatActionResult> openFightWithAbility,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken = default
    ) =>
        streamer.StreamTurn(
            session,
            ct => ResolveTurn(session, targetId, abilityName, ct),
            cancellationToken
        );

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        CancellationToken cancellationToken
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
            return new GameTurnPrompt.Reply($"Ability {abilityName} not found");
        }

        try
        {
            return ability is AttackAbility
                ? await OpenFight(session, targetId, abilityName, gameTime, cancellationToken)
                : await CastSupport(session, targetId, abilityName, gameTime, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new GameTurnPrompt.Reply(ex.Message);
        }
    }

    private async Task<GameTurnPrompt> CastSupport(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var result = await castAbility.Handle(
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

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        var fact = new AbilityCastFact(
            result.AbilityName,
            result.AbilityDescription,
            result.TargetName,
            result.TargetIsPlayer
        );

        return new GameTurnPrompt.Narrate(BuildNarrationPrompt(fact), IncludeTools: false);
    }

    private async Task<GameTurnPrompt> OpenFight(
        GameTurnSession session,
        Guid targetId,
        string abilityName,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var result = await openFightWithAbility.Handle(
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

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                GameTime = gameTime,
            },
            cancellationToken
        );

        if (result.CombatResult.Outcome == CombatOutcome.Ongoing)
        {
            return new GameTurnPrompt.None();
        }

        var fact = new CombatConclusionFact(result.CombatResult.Outcome, result.OpponentNames);
        return new GameTurnPrompt.Narrate(
            StreamCombatActionTurnHandler.BuildNarrationPrompt(fact),
            IncludeTools: false
        );
    }

    internal static string BuildNarrationPrompt(AbilityCastFact fact)
    {
        var json = JsonSerializer.Serialize(fact, Common.Serialization.TrpgJsonOptions.Default);

        return $"""
            The player has just cast a support ability. Result: {json}.
            The ability has already taken effect; narrate the casting and its visible effect in a
            sentence or two. Do not invent effects beyond the description. Do not call any tools.
            """;
    }
}
