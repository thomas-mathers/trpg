using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class PlayerRespawner(
    ICommandHandler<ResolvePlayerRespawnCommand, PlayerRespawnFact> resolvePlayerRespawn,
    ICommandHandler<RestoreCreatureResourcesCommand> restoreCreatureResources,
    ICommandHandler<ReviveCreaturesCommand> reviveCreatures,
    ICommandHandler<MovePlayerCommand> movePlayer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime
)
{
    public async Task Respawn(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    )
    {
        var gameTime = await getGameTime.Handle(
            new GetGameTimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        using (
            var transaction = new TransactionScope(
                TransactionScopeOption.Required,
                TransactionScopeAsyncFlowOption.Enabled
            )
        )
        {
            var fact = await resolvePlayerRespawn.Handle(
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

            await reviveCreatures.Handle(
                new ReviveCreaturesCommand { CreatureIds = [session.PlayerId] },
                cancellationToken
            );

            await movePlayer.Handle(
                new MovePlayerCommand
                {
                    PlayerId = session.PlayerId,
                    DestinationLocationId = fact.SanctuaryLocationId,
                    GameTime = gameTime,
                },
                cancellationToken
            );

            transaction.Complete();
        }

        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = session.PlayerId,
                Encounter = startedEncounter,
                GameTime = gameTime,
            },
            cancellationToken
        );
    }
}
