using System.Text.Json;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal record CombatConclusionFact(
    CombatOutcome Outcome,
    IReadOnlyCollection<string> OpponentNames
);

internal class StreamCombatConclusionNarrationTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetFightByIdQuery, FightEncounter?> getFightById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        Guid fightId,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, fightId, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        Guid fightId,
        CancellationToken cancellationToken
    )
    {
        var fight = await getFightById.Handle(
            new GetFightByIdQuery { FightId = fightId },
            cancellationToken
        );

        if (fight == null || fight.WorldId != session.WorldId || fight.PlayerId != session.PlayerId)
        {
            return new GameTurnPrompt.Reply("There's no fight to narrate the conclusion of.");
        }

        var combatants = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = fight.CombatantIds },
            cancellationToken
        );
        var opponentNames = fight
            .CombatantIds.Where(id => id != session.PlayerId)
            .Select(id => combatants.GetValueOrDefault(id)?.Name)
            .Where(name => name != null)
            .Select(name => name!)
            .ToArray();

        var fact = new CombatConclusionFact(fight.Outcome, opponentNames);

        return new GameTurnPrompt.Narrate(BuildNarrationPrompt(fact), IncludeTools: false);
    }

    private static string BuildNarrationPrompt(CombatConclusionFact fact)
    {
        var json = JsonSerializer.Serialize(fact, Common.Serialization.TrpgJsonOptions.Default);

        return fact.Outcome switch
        {
            CombatOutcome.Victory => $"""
                The fight has concluded. Result: {json}.
                Every listed opponent is dead — do not depict any of them as alive, conscious, or
                speaking afterward. Narrate the fight's conclusion vividly: the killing blow and its
                immediate aftermath. Do not call any tools.
                """,
            CombatOutcome.Defeat => $"""
                The fight has concluded. Result: {json}.
                The player has been struck down and lost consciousness; the opponent(s) survive.
                Narrate only up to the moment the player blacks out — what happens next is handled
                separately, so do not narrate anything after that. Do not call any tools.
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(fact)),
        };
    }
}
