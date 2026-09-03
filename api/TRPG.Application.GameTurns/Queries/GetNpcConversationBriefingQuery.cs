using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Results;
using TRPG.Application.Reputations;
using TRPG.Application.Reputations.Mappers;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.RoomBookings.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns.Queries;

public class GetNpcConversationBriefingQuery
{
    public required Guid NpcId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
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

public record NpcConversationReputationEvent(string Text);

public record NpcConversationHistoryResult(
    string Summary,
    IReadOnlyCollection<NpcConversationRecord> Recent,
    IReadOnlyCollection<NpcConversationDurableFact> DurableFacts,
    IReadOnlyCollection<NpcConversationOpenThread> OpenThreads,
    IReadOnlyCollection<NpcConversationObservedCrime> ObservedCrimes,
    IReadOnlyCollection<NpcConversationReputationEvent> ReputationHistory
);

public record NpcConversationRoomBookingStatus(bool HasActiveBooking, string? RoomName);

public record NpcConversationQuest(string Name);

public record NpcConversationQuests(
    IReadOnlyCollection<NpcConversationQuest> Available,
    IReadOnlyCollection<NpcConversationQuest> Active,
    IReadOnlyCollection<NpcConversationQuest> ReadyToComplete,
    IReadOnlyCollection<NpcConversationQuest> Completed
);

public record NpcConversationRuntimeState(
    CreatureState State,
    NpcConversationAttitude Attitude,
    NpcConversationHistoryResult ConversationHistory,
    NpcConversationQuests Quests,
    NpcConversationRoomBookingStatus? RoomBooking
);

public record NpcConversationBriefing(
    NpcConversationIdentity Identity,
    NpcConversationAppearance Appearance,
    NpcConversationBehavior Behavior,
    NpcConversationBackground PrivateBackground,
    NpcConversationRuntimeState RuntimeState
);

internal class GetNpcConversationBriefingQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetQuestInteractionsForGiverQuery, QuestInteractionsResult> getQuestInteractions,
    IQueryHandler<GetEffectiveReputationQuery, int> getEffectiveReputation,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    IQueryHandler<
        GetCrimesWitnessedByCreatureQuery,
        IReadOnlyList<WitnessedCrime>
    > getCrimesWitnessedByCreature,
    IQueryHandler<GetCreatureProfileByCreatureIdQuery, CreatureProfile?> getCreatureProfile,
    IQueryHandler<
        GetNpcConversationHistoryQuery,
        NpcConversationHistory?
    > getNpcConversationHistory,
    IQueryHandler<
        GetRecentNpcConversationsQuery,
        IReadOnlyList<NpcConversation>
    > getRecentNpcConversations,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetTradeWorkstationByBuildingIdQuery,
        Workstation?
    > getTradeWorkstationByBuildingId,
    IQueryHandler<
        GetRoomBookingsForPlayerInBuildingQuery,
        IReadOnlyCollection<RoomBooking>
    > getRoomBookingsForPlayerInBuilding,
    IQueryHandler<GetRoomsByIdsQuery, IReadOnlyDictionary<Guid, Room>> getRoomsByIds
) : IQueryHandler<GetNpcConversationBriefingQuery, NpcConversationBriefing>
{
    private const int ReputationHistoryLimit = 5;

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
            await getCreatureProfile.Handle(
                new GetCreatureProfileByCreatureIdQuery
                {
                    CreatureId = query.NpcId,
                    WorldId = query.WorldId,
                },
                cancellationToken
            )
            ?? new CreatureProfile
            {
                CreatureId = npc.Id,
                WorldId = npc.WorldId,
                Description = npc.Biography,
            };

        var history = await getNpcConversationHistory.Handle(
            new GetNpcConversationHistoryQuery { CreatureId = query.PlayerId, NpcId = query.NpcId },
            cancellationToken
        );

        var recentConversations =
            history == null
                ? []
                : await getRecentNpcConversations.Handle(
                    new GetRecentNpcConversationsQuery
                    {
                        NpcConversationHistoryId = history.Id,
                        Limit = 5,
                    },
                    cancellationToken
                );
        var recent = recentConversations
            .Select(conversation => new NpcConversationRecord(conversation.Summary))
            .ToArray();

        var attitude = await GetAttitude(query, cancellationToken);
        var quests = await GetQuests(query, cancellationToken);
        var observedCrimes = await GetObservedCrimes(query, cancellationToken);
        var reputationHistory = await GetReputationHistory(query, cancellationToken);
        var roomBooking = await GetRoomBookingStatus(query, cancellationToken);

        return new NpcConversationBriefing(
            new NpcConversationIdentity(
                npc.Name,
                npc.CreatureType.ToString(),
                npc.Gender,
                GameClock.EpochYear - npc.BirthYear
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
                npc.State,
                attitude,
                new NpcConversationHistoryResult(
                    history?.Summary ?? "",
                    recent,
                    ToActiveFacts(history),
                    ToActiveThreads(history),
                    observedCrimes,
                    reputationHistory
                ),
                quests,
                roomBooking
            )
        );
    }

    private async Task<NpcConversationRoomBookingStatus?> GetRoomBookingStatus(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = query.LocationId },
            cancellationToken
        );
        if (building is not { BuildingType: BuildingType.Inn })
        {
            return null;
        }

        var workstation = await getTradeWorkstationByBuildingId.Handle(
            new GetTradeWorkstationByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );
        var isInnkeeperOrStaff =
            workstation?.OwnerCreatureId == query.NpcId || workstation?.OccupantId == query.NpcId;
        if (!isInnkeeperOrStaff)
        {
            return null;
        }

        var bookings = await getRoomBookingsForPlayerInBuilding.Handle(
            new GetRoomBookingsForPlayerInBuildingQuery
            {
                PlayerId = query.PlayerId,
                BuildingId = building.Id,
            },
            cancellationToken
        );
        var booking = bookings.FirstOrDefault();
        if (booking == null)
        {
            return new NpcConversationRoomBookingStatus(false, null);
        }

        var rooms = await getRoomsByIds.Handle(
            new GetRoomsByIdsQuery { Ids = [booking.RoomId] },
            cancellationToken
        );
        return new NpcConversationRoomBookingStatus(
            true,
            rooms.GetValueOrDefault(booking.RoomId)?.Name
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
        var witnessedCrimes = await getCrimesWitnessedByCreature.Handle(
            new GetCrimesWitnessedByCreatureQuery
            {
                WorldId = query.WorldId,
                WitnessCreatureId = query.NpcId,
                PlayerId = query.PlayerId,
            },
            cancellationToken
        );

        return witnessedCrimes.Select(ToObservedCrime).ToArray();
    }

    private static NpcConversationObservedCrime ToObservedCrime(WitnessedCrime crime)
    {
        if (crime.Kind == WitnessedCrimeKind.Kill)
        {
            return new NpcConversationObservedCrime(
                $"You witnessed the player kill {crime.SubjectName}."
            );
        }

        var text =
            crime.Outcome == TheftCrimeOutcome.Apologized
                ? $"You witnessed the player steal from {crime.SubjectName}, though they later apologized and made it right."
                : $"You witnessed the player steal from {crime.SubjectName}.";
        return new NpcConversationObservedCrime(text);
    }

    private async Task<NpcConversationReputationEvent[]> GetReputationHistory(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    )
    {
        var factionIdsByCreature = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = [query.NpcId] },
            cancellationToken
        );
        var factionIds = factionIdsByCreature.GetValueOrDefault(query.NpcId, []);

        var targets = new List<ReputationLogTarget>
        {
            new(query.NpcId, ReputationTargetType.Creature),
        };
        targets.AddRange(
            factionIds.Select(factionId => new ReputationLogTarget(
                factionId,
                ReputationTargetType.Faction
            ))
        );

        var entries = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = query.PlayerId,
                Targets = targets,
                Limit = ReputationHistoryLimit,
            },
            cancellationToken
        );

        return entries
            .Where(entry => !IsCivicRecord(entry.Reason))
            .Select(entry => new NpcConversationReputationEvent(
                entry.Detail ?? entry.Reason.ToDisplayText()
            ))
            .ToArray();
    }

    // Fines and jail time are processed by the guard/court system, not spread by witnesses or
    // gossip, so ordinary NPCs have no plausible way of knowing about them.
    private static bool IsCivicRecord(ReputationReason reason) =>
        reason is ReputationReason.PaidFineToGuard or ReputationReason.ServedJailTime;

    private async Task<NpcConversationAttitude> GetAttitude(
        GetNpcConversationBriefingQuery query,
        CancellationToken cancellationToken
    )
    {
        var score = await getEffectiveReputation.Handle(
            new GetEffectiveReputationQuery
            {
                ObserverCreatureId = query.PlayerId,
                TargetCreatureId = query.NpcId,
            },
            cancellationToken
        );

        return ReputationAttitudeCalculator.FromScore(score) switch
        {
            ReputationAttitude.Hostile => new NpcConversationAttitude(
                "Hostile",
                "Openly hostile and unwilling to help."
            ),
            ReputationAttitude.Wary => new NpcConversationAttitude(
                "Wary",
                "Suspicious, guarded, and reluctant to cooperate."
            ),
            ReputationAttitude.Neutral => new NpcConversationAttitude(
                "Neutral",
                "Civil but not personally invested."
            ),
            ReputationAttitude.Warm => new NpcConversationAttitude(
                "Warm",
                "Openly appreciative and willing to help."
            ),
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
