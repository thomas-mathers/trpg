using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Queries;
using TRPG.Application.Knowledge.Commands;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Commands;

public record ShareExpeditionDiscoveryCommand(
    Guid WorldId,
    Guid SessionId,
    Guid PlayerId,
    Guid ExpeditionId
);

internal class ShareExpeditionDiscoveryCommandHandler(
    IQueryHandler<GetDungeonExpeditionsQuery, IReadOnlyList<DungeonExpedition>> getExpeditions,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreature,
    IQueryHandler<GetGameSessionQuery, GameSession> getSession,
    IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>> getOpenConversations,
    IQueryHandler<GetKnownSecretIdsQuery, IReadOnlyList<Guid>> getKnownSecrets,
    ICommandHandler<LearnSecretCommand, bool> learnSecret,
    IQueryHandler<GetDungeonConversationKnowledgeQuery, DungeonConversationKnowledge?> getKnowledge
) : ICommandHandler<ShareExpeditionDiscoveryCommand, DungeonConversationKnowledge?>
{
    public async Task<DungeonConversationKnowledge?> Handle(
        ShareExpeditionDiscoveryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var expeditions = await getExpeditions.Handle(
            new GetDungeonExpeditionsQuery(command.WorldId),
            cancellationToken
        );
        var expedition =
            expeditions.SingleOrDefault(expedition => expedition.Id == command.ExpeditionId)
            ?? throw new InvalidOperationException("This expedition is not in the current world.");
        await ValidateConversation(command, expedition, cancellationToken);
        var knownSecrets = await getKnownSecrets.Handle(
            new GetKnownSecretIdsQuery(WorldId: command.WorldId, KnowerId: command.PlayerId),
            cancellationToken
        );
        if (!knownSecrets.Contains(expedition.DiscoverySecretId))
            throw new InvalidOperationException("Read the journal before sharing its account.");

        await learnSecret.Handle(
            new LearnSecretCommand
            {
                WorldId = command.WorldId,
                KnowerId = expedition.SurvivorId,
                SecretId = expedition.DiscoverySecretId,
            },
            cancellationToken
        );
        return await getKnowledge.Handle(
            new GetDungeonConversationKnowledgeQuery(
                WorldId: command.WorldId,
                PlayerId: command.PlayerId,
                NpcId: expedition.SurvivorId
            ),
            cancellationToken
        );
    }

    private async Task ValidateConversation(
        ShareExpeditionDiscoveryCommand command,
        DungeonExpedition expedition,
        CancellationToken cancellationToken
    )
    {
        var session = await getSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (session.WorldId != command.WorldId || session.PlayerId != command.PlayerId)
            throw new InvalidOperationException(
                "The conversation session does not belong to this player and world."
            );

        var player = await getCreature.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        var survivor = await getCreature.Handle(
            new GetCreatureByIdQuery { Id = expedition.SurvivorId },
            cancellationToken
        );
        if (
            player == null
            || survivor == null
            || player.WorldId != command.WorldId
            || survivor.WorldId != command.WorldId
            || player.State == CreatureState.Dead
            || survivor.State == CreatureState.Dead
            || survivor.State == CreatureState.Sleeping
            || player.LocationId != survivor.LocationId
        )
            throw new InvalidOperationException(
                "The survivor must be awake and alive beside the player."
            );

        var conversations = await getOpenConversations.Handle(
            new GetOpenNpcConversationsQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (!conversations.ContainsValue(survivor.Id))
            throw new InvalidOperationException("Start a conversation with the survivor first.");
    }
}
