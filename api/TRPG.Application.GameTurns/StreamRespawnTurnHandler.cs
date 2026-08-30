using System.Text.Json;
using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamRespawnTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<ResolvePlayerRespawnCommand, PlayerRespawnFact> resolvePlayerRespawn,
    ICommandHandler<RestoreCreatureResourcesCommand> restoreCreatureResources,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<MovePlayerCommand, MovePlayerResult> movePlayer
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );

        if (player?.State != CreatureState.Dead)
        {
            return new GameTurnPrompt.Reply("There's nothing to respawn from right now.");
        }

        PlayerRespawnFact fact;

        using (
            var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            fact = await resolvePlayerRespawn.Handle(
                new ResolvePlayerRespawnCommand
                {
                    WorldId = session.WorldId,
                    PlayerId = session.PlayerId,
                },
                cancellationToken
            );

            await restoreCreatureResources.Handle(
                new RestoreCreatureResourcesCommand { CreatureIds = [session.PlayerId] },
                cancellationToken
            );

            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = [session.PlayerId],
                    State = CreatureState.Idle,
                },
                cancellationToken
            );

            await movePlayer.Handle(
                new MovePlayerCommand
                {
                    PlayerId = session.PlayerId,
                    SessionId = session.SessionId,
                    DestinationLocationId = fact.SanctuaryLocationId,
                },
                cancellationToken
            );

            transaction.Complete();
        }

        return new GameTurnPrompt.Narrate(BuildNarrationPrompt(fact), IncludeTools: false);
    }

    private static string BuildNarrationPrompt(PlayerRespawnFact fact)
    {
        var json = JsonSerializer.Serialize(fact, Common.Serialization.TrpgJsonOptions.Default);

        return $"""
            The player died and has been revived at a temple in {fact.TempleCityName}. Result: {json}.
            Narrate the player waking in the temple's Sanctuary, healed of their wounds. Only describe
            a cleric tending to them if IsClericPresent is true (in which case ClericName is who) —
            otherwise describe the Sanctuary as quiet and unattended. Briefly acknowledge that their
            belongings were left behind at {fact.DeathLocationName} and must be recovered. Do not call
            any tools.
            """;
    }
}
