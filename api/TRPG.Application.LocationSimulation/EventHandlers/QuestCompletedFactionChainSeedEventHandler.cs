using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.EventHandlers;

// Joining a faction through its initiation quest is the moment the player becomes eligible for
// that faction's story content, so the follow-up chain is seeded right away rather than waiting
// on the ambient per-city-entrance roll to eventually land on this faction.
internal sealed class QuestCompletedFactionChainSeedEventHandler(
    IQueryHandler<GetQuestByIdQuery, Quest?> getQuestById,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<SeedLlmQuestChainCommand, bool> seedLlmQuestChain
) : IDomainEventConsumer<QuestCompletedEvent>
{
    public async Task Handle(
        QuestCompletedEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var quest = await getQuestById.Handle(
            new GetQuestByIdQuery { Id = domainEvent.QuestId },
            cancellationToken
        );
        if (quest?.MembershipRewardFactionId is not { } factionId)
        {
            return;
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = domainEvent.PlayerId },
            cancellationToken
        );
        if (player == null)
        {
            return;
        }

        await seedLlmQuestChain.Handle(
            new SeedLlmQuestChainCommand
            {
                WorldId = domainEvent.WorldId,
                PlayerId = domainEvent.PlayerId,
                LocationId = player.LocationId,
                PlayerLevel = player.Level,
                GiverFactionId = factionId,
            },
            cancellationToken
        );
    }
}
