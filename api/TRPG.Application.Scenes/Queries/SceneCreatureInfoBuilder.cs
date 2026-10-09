using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Routing.Queries;
using TRPG.Application.Scenes.Boundaries;
using TRPG.Application.Scenes.Neighbors;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Scenes.Roads;
using TRPG.Application.Weather.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.Worlds.Results;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Queries;

internal sealed class SceneCreatureInfoBuilder(
    IQueryHandler<
        GetEffectiveReputationsQuery,
        IReadOnlyDictionary<Guid, int>
    > getEffectiveReputations,
    IQueryHandler<GetQuestMarkersForCreaturesQuery, QuestMarkersResult> getQuestMarkersForCreatures,
    IQueryHandler<
        GetTotalCharacterXpFromSkillsQuery,
        IReadOnlyDictionary<Guid, int>
    > getTotalCharacterXpFromSkills,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds,
    IQueryHandler<GetFactionsByIdsQuery, IReadOnlyDictionary<Guid, Faction>> getFactionsByIds,
    IQueryHandler<
        GetTradeWorkstationIdsByOccupantIdsQuery,
        IReadOnlyDictionary<Guid, Guid?>
    > getTradeWorkstationIdsByOccupantIds,
    IQueryHandler<
        GetCreatureJourneyPositionsQuery,
        IReadOnlyDictionary<Guid, CreatureJourneyPosition>
    > getCreatureJourneyPositions
)
{
    public async Task<IReadOnlyCollection<SceneCreatureInfo>> BuildNearbyPeopleInfos(
        GetSceneQuery query,
        Guid locationId,
        IReadOnlyCollection<CreatureResult> nearby,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>> equippedItemsByCreature,
        CancellationToken cancellationToken
    )
    {
        if (nearby.Count == 0)
        {
            return [];
        }

        var journeyPositions = await getCreatureJourneyPositions.Handle(
            new GetCreatureJourneyPositionsQuery
            {
                LocationId = locationId,
                GameTime = query.GameTime,
                CreatureIds = nearby.Select(creature => creature.Id).ToArray(),
            },
            cancellationToken
        );
        var present = nearby;
        if (present.Count == 0)
        {
            return [];
        }

        var nearbyCreatureIds = present.Select(x => x.Id).ToArray();
        var factionIdsByCreature = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = nearbyCreatureIds },
            cancellationToken
        );
        var allFactionIds = factionIdsByCreature.Values.SelectMany(ids => ids).Distinct().ToArray();
        var factionsById = await getFactionsByIds.Handle(
            new GetFactionsByIdsQuery { Ids = allFactionIds },
            cancellationToken
        );

        var factionNamesByCreature = factionIdsByCreature.ToDictionary(
            kv => kv.Key,
            kv =>
                (IReadOnlyList<string>)
                    kv
                        .Value.Where(id =>
                            factionsById.TryGetValue(id, out var f) && !f.IsCityFaction
                        )
                        .Select(id => factionsById[id].Name)
                        .ToArray()
        );

        var reputationByCreature = await getEffectiveReputations.Handle(
            new GetEffectiveReputationsQuery
            {
                ObserverCreatureId = query.PlayerId,
                TargetCreatureIds = nearbyCreatureIds,
                FactionIdsByCreature = factionIdsByCreature,
            },
            cancellationToken
        );
        var tradeWorkstationIdsByCreature = await getTradeWorkstationIdsByOccupantIds.Handle(
            new GetTradeWorkstationIdsByOccupantIdsQuery { OccupantIds = nearbyCreatureIds },
            cancellationToken
        );
        var questMarkers = await getQuestMarkersForCreatures.Handle(
            new GetQuestMarkersForCreaturesQuery
            {
                PlayerId = query.PlayerId,
                WorldId = query.WorldId,
                CreatureIds = nearbyCreatureIds,
            },
            cancellationToken
        );

        var xpTotalsByCreature = await getTotalCharacterXpFromSkills.Handle(
            new GetTotalCharacterXpFromSkillsQuery { CreatureIds = nearbyCreatureIds },
            cancellationToken
        );
        return present
            .Select(x =>
                BuildSceneCreatureInfo(
                    x,
                    query.CurrentDate.Year,
                    factionNames: factionNamesByCreature.GetValueOrDefault(x.Id, []),
                    condition: x.Condition,
                    activity: x.Activity,
                    posture: x.Posture,
                    movement: x.Movement,
                    reputation: reputationByCreature.GetValueOrDefault(x.Id, 0),
                    totalCharacterXp: xpTotalsByCreature.GetValueOrDefault(x.Id, 0),
                    placement: ToPlacement(x, journeyPositions.GetValueOrDefault(x.Id)),
                    equipment: equippedItemsByCreature
                        .GetValueOrDefault(x.Id, [])
                        .ToVisualEquipment(),
                    tradeWorkstationId: tradeWorkstationIdsByCreature.GetValueOrDefault(x.Id),
                    questMarkers: questMarkers.EntriesByCreatureId.GetValueOrDefault(x.Id, []),
                    readyToDeliver: questMarkers.ReadyToDeliverCreatureIds.Contains(x.Id),
                    walk: ToWalk(journeyPositions.GetValueOrDefault(x.Id), query.GameTime)
                )
            )
            .ToArray();
    }

    private static Placement ToPlacement(
        CreatureResult creature,
        CreatureJourneyPosition? journeyPosition
    ) =>
        journeyPosition is { } position
            ? new Placement(position.Position.X, position.Position.Y, creature.Angle)
            : new Placement(creature.X, creature.Y, creature.Angle);

    private static SceneCreatureWalk? ToWalk(
        CreatureJourneyPosition? journeyPosition,
        GameInstant now
    ) =>
        journeyPosition is not { } position
            ? null
            : new SceneCreatureWalk(
                position.Path,
                now,
                position.MetersPerGameSecond,
                position.LeavesAtEnd,
                position.PausedAt is null ? null : now
            );

    public static SceneCreatureInfo BuildSceneCreatureInfo(
        CreatureResult creature,
        int currentYear,
        IReadOnlyCollection<string> factionNames,
        CreatureCondition condition,
        CreatureActivity? activity,
        CreaturePosture posture,
        CreatureMovement movement,
        int? reputation,
        int totalCharacterXp,
        Placement placement,
        IReadOnlyCollection<SceneEquipmentVisual>? equipment = null,
        Guid? tradeWorkstationId = null,
        IReadOnlyCollection<QuestMarkerEntry>? questMarkers = null,
        bool readyToDeliver = false,
        SceneJourneyInfo? journey = null,
        SceneCreatureWalk? walk = null
    )
    {
        var experienceProgress = SkillFormulas.GetExperienceProgress(
            creature.Level,
            totalCharacterXp
        );

        return new SceneCreatureInfo(
            creature.Id,
            creature.Name,
            creature.CreatureType,
            creature.Gender,
            creature.Profession,
            creature.Level,
            currentYear - creature.BirthYear,
            factionNames,
            condition,
            activity,
            posture,
            movement,
            creature.IsSneaking,
            creature.IsAlerted,
            creature.IsRestrained,
            reputation,
            creature.Gold,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp,
            creature.CurrentMp,
            creature.MaximumMp,
            experienceProgress.Current,
            experienceProgress.ToNextLevel,
            creature.Strength,
            creature.Dexterity,
            creature.Intelligence,
            creature.Endurance,
            creature.Stamina,
            creature.Mana,
            creature.Defense,
            creature.MovementSpeed,
            creature.PhysicalResistance,
            creature.FireResistance,
            creature.IceResistance,
            creature.LightningResistance,
            creature.PoisonResistance,
            creature.MagicResistance,
            tradeWorkstationId,
            questMarkers ?? [],
            readyToDeliver,
            creature.Effects,
            journey,
            placement
        )
        {
            Equipment = equipment ?? [],
            Walk = walk,
        };
    }
}
