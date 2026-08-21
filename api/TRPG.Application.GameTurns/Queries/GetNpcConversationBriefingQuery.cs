using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Application.Worlds;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public class GetNpcConversationBriefingQuery
{
    public required Guid NpcId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

public record NpcConversationIdentity(string Name, string Race, Gender Gender, int Age);

public record NpcConversationAppearance(
    string Description,
    IReadOnlyCollection<string> DistinguishingFeatures
);

public record NpcConversationBehavior(string Personality, string SpeechStyle, string Hobby);

public record NpcConversationFamilyMember(string Name, string Relationship);

public record NpcConversationWork(
    string Building,
    bool IsOwner,
    string Hours,
    IReadOnlyCollection<string> DaysOff
);

public record NpcConversationBackground(
    string Origin,
    string? Profession,
    IReadOnlyCollection<string> Factions,
    IReadOnlyCollection<NpcConversationFamilyMember> Family,
    NpcConversationWork? Work,
    string? Home
);

public record NpcConversationAttitude(string Disposition, string Guidance);

public record NpcConversationRecord(string Summary);

public record NpcConversationDurableFact(int Id, string Text);

public record NpcConversationOpenThread(int Id, string Text);

public record NpcConversationObservedCrime(string Text);

internal record ObservedCrime(DateTime OccurredAt, string Text);

public record NpcConversationHistoryResult(
    string Summary,
    IReadOnlyCollection<NpcConversationRecord> Recent,
    IReadOnlyCollection<NpcConversationDurableFact> DurableFacts,
    IReadOnlyCollection<NpcConversationOpenThread> OpenThreads,
    IReadOnlyCollection<NpcConversationObservedCrime> ObservedCrimes
);

public record NpcConversationQuest(string Name);

public record NpcConversationQuests(
    IReadOnlyCollection<NpcConversationQuest> Available,
    IReadOnlyCollection<NpcConversationQuest> Active,
    IReadOnlyCollection<NpcConversationQuest> ReadyToComplete,
    IReadOnlyCollection<NpcConversationQuest> Completed
);

public record NpcConversationRuntimeState(
    NpcConversationAttitude Attitude,
    NpcConversationHistoryResult ConversationHistory,
    NpcConversationQuests Quests
);

public record NpcConversationBriefing(
    NpcConversationIdentity Identity,
    NpcConversationAppearance Appearance,
    NpcConversationBehavior Behavior,
    NpcConversationBackground PrivateBackground,
    NpcConversationRuntimeState RuntimeState
);

internal class GetNpcConversationBriefingQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult> getQuestInteractions
) : IQueryHandler<GetNpcConversationBriefingQuery, NpcConversationBriefing>
{
    public async Task<NpcConversationBriefing> Handle(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var npc =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = query.NpcId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Creature {query.NpcId} not found.");

        var profile =
            await context
                .NpcProfiles.AsNoTracking()
                .FirstOrDefaultAsync(
                    profile =>
                        profile.CreatureId == query.NpcId && profile.WorldId == query.WorldId,
                    cancellationToken
                )
            ?? new NpcProfile
            {
                CreatureId = npc.Id,
                WorldId = npc.WorldId,
                Description = npc.Biography,
            };

        var history = await context
            .NpcConversationHistories.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CreatureId == query.PlayerId && item.NpcId == query.NpcId,
                cancellationToken
            );

        var recent =
            history == null
                ? []
                : await context
                    .NpcConversations.AsNoTracking()
                    .Where(conversation => conversation.NpcConversationHistoryId == history.Id)
                    .OrderByDescending(conversation => conversation.CreatedAt)
                    .Take(5)
                    .OrderBy(conversation => conversation.CreatedAt)
                    .Select(conversation => new NpcConversationRecord(conversation.Summary))
                    .ToArrayAsync(cancellationToken);

        var attitude = await GetAttitude(query, profile, cancellationToken);
        var quests = await GetQuests(query, cancellationToken);
        var observedCrimes = await GetObservedCrimes(query, cancellationToken);

        return new NpcConversationBriefing(
            new NpcConversationIdentity(
                npc.Name,
                npc.CreatureType.ToString(),
                npc.Gender,
                WorldEpoch.Year - npc.BirthYear
            ),
            new NpcConversationAppearance(
                profile.Description,
                profile.Appearance.DistinguishingFeatures
            ),
            new NpcConversationBehavior(
                profile.Behavior.Personality,
                profile.Behavior.SpeechStyle,
                profile.Behavior.Hobby
            ),
            new NpcConversationBackground(
                profile.PrivateBackground.Origin,
                profile.PrivateBackground.Profession,
                profile
                    .PrivateBackground.Factions.Where(faction => !faction.IsCityFaction)
                    .Select(faction => faction.Name)
                    .ToArray(),
                profile
                    .PrivateBackground.Family.Select(member => new NpcConversationFamilyMember(
                        member.Name,
                        member.Relationship
                    ))
                    .ToArray(),
                profile.PrivateBackground.Work is { } work
                    ? new NpcConversationWork(work.Building, work.IsOwner, work.Hours, work.DaysOff)
                    : null,
                profile.PrivateBackground.Home
            ),
            new NpcConversationRuntimeState(
                attitude,
                new NpcConversationHistoryResult(
                    history?.Summary ?? "",
                    recent,
                    ToActiveFacts(history),
                    ToActiveThreads(history),
                    observedCrimes
                ),
                quests
            )
        );
    }

    private static NpcConversationDurableFact[] ToActiveFacts(NpcConversationHistory? history) =>
        history == null
            ? []
            : history
                .DurableFacts.Where(fact => !fact.IsRetracted)
                .Select((fact, index) => new NpcConversationDurableFact(index + 1, fact.Text))
                .ToArray();

    private static NpcConversationOpenThread[] ToActiveThreads(NpcConversationHistory? history) =>
        history == null
            ? []
            : history
                .OpenThreads.Where(thread => !thread.IsResolved)
                .Select((thread, index) => new NpcConversationOpenThread(index + 1, thread.Text))
                .ToArray();

    private async Task<NpcConversationObservedCrime[]> GetObservedCrimes(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    )
    {
        var kills = await GetObservedKills(query, cancellationToken);
        var thefts = await GetObservedThefts(query, cancellationToken);

        return
        [
            .. kills
                .Concat(thefts)
                .OrderByDescending(crime => crime.OccurredAt)
                .Select(crime => new NpcConversationObservedCrime(crime.Text)),
        ];
    }

    private async Task<ObservedCrime[]> GetObservedKills(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    ) =>
        await context
            .CrimeWitnesses.AsNoTracking()
            .Where(witness =>
                witness.WorldId == query.WorldId
                && witness.CreatureId == query.NpcId
                && witness.Resolution != CrimeWitnessResolution.Silenced
            )
            .Join(
                context.Crimes.OfType<KillCrime>().AsNoTracking(),
                witness => witness.CrimeId,
                crime => crime.Id,
                (witness, crime) =>
                    new
                    {
                        crime.PlayerId,
                        crime.VictimName,
                        crime.OccurredAt,
                    }
            )
            .Where(crime => crime.PlayerId == query.PlayerId)
            .OrderByDescending(crime => crime.OccurredAt)
            .Select(crime => new ObservedCrime(
                crime.OccurredAt,
                $"You witnessed the player kill {crime.VictimName}."
            ))
            .ToArrayAsync(cancellationToken);

    private async Task<ObservedCrime[]> GetObservedThefts(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    ) =>
        await context
            .CrimeWitnesses.AsNoTracking()
            .Where(witness =>
                witness.WorldId == query.WorldId
                && witness.CreatureId == query.NpcId
                && witness.Resolution != CrimeWitnessResolution.Silenced
            )
            .Join(
                context.Crimes.OfType<TheftCrime>().AsNoTracking(),
                witness => witness.CrimeId,
                crime => crime.Id,
                (witness, crime) =>
                    new
                    {
                        crime.PlayerId,
                        crime.OwnerName,
                        crime.OccurredAt,
                    }
            )
            .Where(crime => crime.PlayerId == query.PlayerId)
            .OrderByDescending(crime => crime.OccurredAt)
            .Select(crime => new ObservedCrime(
                crime.OccurredAt,
                $"You witnessed the player steal from {crime.OwnerName}."
            ))
            .ToArrayAsync(cancellationToken);

    private async Task<NpcConversationAttitude> GetAttitude(
        GetNpcConversationBriefingQuery query,
        NpcProfile profile,
        CancellationToken cancellationToken
    )
    {
        var factionIds = profile.PrivateBackground.Factions.Select(faction => faction.Id).ToArray();

        var scores = await context
            .Reputations.AsNoTracking()
            .Where(reputation =>
                reputation.CreatureId == query.PlayerId
                && (
                    (
                        reputation.TargetType == ReputationTargetType.Creature
                        && reputation.TargetId == query.NpcId
                    )
                    || (
                        reputation.TargetType == ReputationTargetType.Faction
                        && factionIds.AsEnumerable().Contains(reputation.TargetId)
                    )
                )
            )
            .ToArrayAsync(cancellationToken);

        var score = Math.Clamp(scores.Sum(reputation => reputation.Score), -100, 100);

        return score switch
        {
            <= -50 => new NpcConversationAttitude(
                "Hostile",
                "Openly hostile and unwilling to help."
            ),
            <= -15 => new NpcConversationAttitude(
                "Wary",
                "Suspicious, guarded, and reluctant to cooperate."
            ),
            < 15 => new NpcConversationAttitude("Neutral", "Civil but not personally invested."),
            < 50 => new NpcConversationAttitude("Warm", "Openly appreciative and willing to help."),
            _ => new NpcConversationAttitude(
                "Trusting",
                "Warm, candid, and inclined to trust the player."
            ),
        };
    }

    private async Task<NpcConversationQuests> GetQuests(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    )
    {
        var interactions = await getQuestInteractions.Handle(
            new GetQuestInteractionsForGiverQuery
            {
                GiverId = query.NpcId,
                PlayerId = query.PlayerId,
                WorldId = query.WorldId,
            },
            cancellationToken
        );

        return new NpcConversationQuests(
            ToNames(interactions.AvailableQuests),
            ToNames(interactions.ActiveQuests),
            ToNames(interactions.ReadyToCompleteQuests),
            ToNames(interactions.CompletedQuests)
        );
    }

    private static NpcConversationQuest[] ToNames(
        IReadOnlyCollection<QuestConversationResult> quests
    ) => quests.Select(quest => new NpcConversationQuest(quest.Name)).ToArray();
}
