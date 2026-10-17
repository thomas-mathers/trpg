using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Abilities.Mappers;
using TRPG.Abilities.Responses;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Application.Quests.Queries;
using TRPG.Application.WorldSimulation;
using TRPG.Creatures.Mappers;
using TRPG.Creatures.Queries;
using TRPG.Creatures.Requests;
using TRPG.Creatures.Responses;
using TRPG.Domain.Models;
using TRPG.GameSessions.Commands;
using TRPG.Inventory.Mappers;
using TRPG.Inventory.Requests;
using TRPG.Inventory.Responses;

namespace TRPG.Creatures.Endpoints;

internal static class CreatureEndpoints
{
    public static void MapCreatureEndpoints(this WebApplication app)
    {
        app.MapGet("/creatures/{creatureId:guid}/abilities", GetAbilities)
            .WithName("GetCreatureAbilities");
        app.MapGet("/creatures/{creatureId:guid}/inventory", GetInventory)
            .WithName("GetCreatureInventory");
        app.MapGet("/creatures/{creatureId:guid}/consumables", GetConsumables)
            .WithName("GetCreatureConsumables");
        app.MapGet("/creatures/{creatureId:guid}/attribute-points", GetAttributePoints)
            .WithName("GetCreatureAttributePoints");
        app.MapPatch("/creatures/{creatureId:guid}/attribute-points", AllocateAttributePoints)
            .WithName("AllocateCreatureAttributePoints");
        app.MapGet("/creatures/{creatureId:guid}/base-attributes", GetBaseAttributes)
            .WithName("GetCreatureBaseAttributes");
        app.MapGet("/creatures/{creatureId:guid}/attributes", GetEffectiveStats)
            .WithName("GetCreatureAttributes");
        app.MapGet("/creatures/{creatureId:guid}/basic-attack-damage", GetBasicAttackDamage)
            .WithName("GetCreatureBasicAttackDamage");
        app.MapGet("/creatures/{creatureId:guid}/skills", GetSkills).WithName("GetCreatureSkills");
        app.MapGet("/creatures/{creatureId:guid}/level", GetLevel).WithName("GetCreatureLevel");
        app.MapPut("/creatures/{creatureId:guid}/equipment", EquipItem)
            .WithName("EquipCreatureItem");
        app.MapDelete("/creatures/{creatureId:guid}/equipment/{slot}", UnequipItem)
            .WithName("UnequipCreatureItem");
        app.MapGet("/creatures/{creatureId:guid}/equipment/preview", GetEquipItemStats)
            .WithName("PreviewCreatureEquipment");
        app.MapGet(
                "/creatures/{creatureId:guid}/equipment/preview/basic-attack-damage",
                GetEquipItemBasicAttackDamage
            )
            .WithName("PreviewCreatureBasicAttackDamage");
        app.MapGet("/players/{playerId:guid}/nearby-corpses", GetNearbyCorpses)
            .WithName("GetNearbyCorpses");
        app.MapGet("/players/{playerId:guid}/world-map", GetWorldMap).WithName("GetWorldMap");
        app.MapGet("/players/{playerId:guid}/local-map", GetLocalMap).WithName("GetLocalMap");
        app.MapPut(
                "/players/{playerId:guid}/interactions/{creatureId:guid}",
                BeginCreatureInteraction
            )
            .WithName("BeginCreatureInteraction");
        app.MapDelete(
                "/players/{playerId:guid}/interactions/{creatureId:guid}",
                EndCreatureInteraction
            )
            .WithName("EndCreatureInteraction");
    }

    private static async Task<Ok<AbilitySummary[]>> GetAbilities(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetCreatureAbilitiesQuery, IReadOnlyList<Ability>> getCreatureAbilities,
        CancellationToken cancellationToken
    )
    {
        var abilities = await getCreatureAbilities.Handle(
            new GetCreatureAbilitiesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(abilities.Select(a => a.ToSummary()).ToArray());
    }

    private static async Task<Ok<InventorySummary>> GetInventory(
        Guid creatureId,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventoryByOwner,
        [FromServices]
            IQueryHandler<
            GetActiveQuestItemIdsQuery,
            IReadOnlyCollection<Guid>
        > getActiveQuestItemIds,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );

        var snapshot = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(creatureId, OwnerType.Creature),
            },
            cancellationToken
        );

        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(snapshot.ToSummary(questItemIds, creature?.CarryingCapacity));
    }

    private static async Task<Ok<ConsumableSummary[]>> GetConsumables(
        Guid creatureId,
        [FromServices]
            IQueryHandler<
            GetInventoryItemsByOwnerQuery,
            IReadOnlyList<Item>
        > getInventoryItemsByOwner,
        CancellationToken cancellationToken
    )
    {
        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(creatureId, OwnerType.Creature),
            },
            cancellationToken
        );

        return TypedResults.Ok(
            items.OfType<Consumable>().Select(item => item.ToSummary()).ToArray()
        );
    }

    private static async Task<Ok<NearbyCorpseSummary[]>> GetNearbyCorpses(
        Guid playerId,
        [FromServices]
            IQueryHandler<GetNearbyCorpsesQuery, IReadOnlyList<CorpseResult>> getNearbyCorpses,
        CancellationToken cancellationToken
    )
    {
        var corpses = await getNearbyCorpses.Handle(
            new GetNearbyCorpsesQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(
            corpses.Select(c => new NearbyCorpseSummary(c.Id, c.Name, c.ItemCount)).ToArray()
        );
    }

    private static async Task<Ok<WorldMapResponse>> GetWorldMap(
        Guid playerId,
        [FromServices] IQueryHandler<GetWorldMapQuery, WorldMapResult> getWorldMap,
        CancellationToken cancellationToken
    )
    {
        var map = await getWorldMap.Handle(
            new GetWorldMapQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(
            new WorldMapResponse(
                map.Countries.Select(ToCountryMapResponse).ToArray(),
                map.States.Select(ToStateMapResponse).ToArray(),
                map.Cities.Select(ToCityMapResponse).ToArray(),
                map.Roads.Select(ToRoadMapResponse).ToArray(),
                map.PlayerStateId,
                map.Corpses.Select(ToCorpseMapResponse).ToArray(),
                map.QuestMarkers.Select(ToQuestMapResponse).ToArray()
            )
        );
    }

    private static async Task<Ok<LocalMapResponse>> GetLocalMap(
        Guid playerId,
        [FromServices] IQueryHandler<GetLocalMapQuery, LocalMapResult> getLocalMap,
        CancellationToken cancellationToken
    )
    {
        var map = await getLocalMap.Handle(
            new GetLocalMapQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(map.ToResponse());
    }

    private static PointResponse ToPointResponse(Point point) => new(point.X, point.Y);

    private static CountryMapResponse ToCountryMapResponse(Country country) =>
        new(country.Id, country.Name, country.Boundary.Points.Select(ToPointResponse).ToArray());

    private static StateMapResponse ToStateMapResponse(State state) =>
        new(
            state.Id,
            state.CountryId,
            state.Name,
            state.Description,
            ToPointResponse(state.Center),
            state.Boundary.Points.Select(ToPointResponse).ToArray()
        );

    private static CityMapResponse ToCityMapResponse(City city) =>
        new(city.Id, city.StateId, city.Name, city.IsCapital);

    private static RoadMapResponse ToRoadMapResponse(WorldMapRoad road) =>
        new(road.Id, road.Name, road.OriginStateId, road.DestinationStateId);

    private static CorpseMapResponse ToCorpseMapResponse(WorldMapCorpse corpse) =>
        new(corpse.Id, corpse.Name, corpse.StateId, corpse.ItemCount);

    private static QuestMapResponse ToQuestMapResponse(WorldMapQuestMarker marker) =>
        new(marker.QuestId, marker.ObjectiveName, marker.StateId);

    private static async Task<Ok<AttributePointsResponse>> GetAttributePoints(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetUnallocatedAttributePointsQuery, int> getUnallocatedAttributePoints,
        CancellationToken cancellationToken
    )
    {
        var points = await getUnallocatedAttributePoints.Handle(
            new GetUnallocatedAttributePointsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new AttributePointsResponse(points));
    }

    private static async Task<NoContent> AllocateAttributePoints(
        Guid creatureId,
        AllocateAttributePointsRequest request,
        [FromServices] ICommandHandler<AllocateAttributePointsCommand> allocateAttributePoints,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] WorldSimulationCoordinator simulation,
        CancellationToken cancellationToken
    )
    {
        var creature = await GetExistingCreature(creatureId, getCreatureById, cancellationToken);

        await simulation.Request(
            creature.WorldId,
            (_, token) =>
                allocateAttributePoints.Handle(
                    new AllocateAttributePointsCommand
                    {
                        CreatureId = creatureId,
                        Deltas = request.Deltas.ToDictionary(),
                    },
                    token
                ),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EquipItem(
        Guid creatureId,
        EquipItemRequest request,
        [FromServices] ICommandHandler<EquipInventoryItemCommand> equipInventoryItem,
        [FromServices] IQueryHandler<HasActiveFightQuery, bool> hasActiveFight,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] WorldSimulationCoordinator simulation,
        CancellationToken cancellationToken
    )
    {
        var creature = await GetExistingCreature(creatureId, getCreatureById, cancellationToken);
        if (
            await hasActiveFight.Handle(
                new HasActiveFightQuery { PlayerId = creatureId },
                cancellationToken
            )
        )
        {
            return FightHeldProblem("Equipment can't change during a fight.");
        }

        await simulation.Request(
            creature.WorldId,
            (_, token) =>
                equipInventoryItem.Handle(
                    new EquipInventoryItemCommand
                    {
                        CreatureId = creatureId,
                        ItemId = request.ItemId,
                        Slot = request.Slot.ToDataModel(),
                    },
                    token
                ),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UnequipItem(
        Guid creatureId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices] ICommandHandler<UnequipInventoryItemCommand> unequipInventoryItem,
        [FromServices] IQueryHandler<HasActiveFightQuery, bool> hasActiveFight,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] WorldSimulationCoordinator simulation,
        CancellationToken cancellationToken
    )
    {
        var creature = await GetExistingCreature(creatureId, getCreatureById, cancellationToken);
        if (
            await hasActiveFight.Handle(
                new HasActiveFightQuery { PlayerId = creatureId },
                cancellationToken
            )
        )
        {
            return FightHeldProblem("Equipment can't change during a fight.");
        }

        await simulation.Request(
            creature.WorldId,
            (_, token) =>
                unequipInventoryItem.Handle(
                    new UnequipInventoryItemCommand
                    {
                        CreatureId = creatureId,
                        Slot = slot.ToDataModel(),
                    },
                    token
                ),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<Ok<BaseAttributesResponse>> GetBaseAttributes(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetCreatureBaseAttributesQuery, Attributes> getCreatureBaseAttributes,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureBaseAttributes.Handle(
            new GetCreatureBaseAttributesQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(
            new BaseAttributesResponse(
                attributes.Strength,
                attributes.Defense,
                attributes.Dexterity,
                attributes.Endurance,
                attributes.Stamina,
                attributes.Mana,
                attributes.Intelligence
            )
        );
    }

    private static async Task<Ok<EffectiveAttributesResponse>> GetEffectiveStats(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetCreatureEffectiveStatsQuery, Attributes> getCreatureEffectiveStats,
        CancellationToken cancellationToken
    )
    {
        var attributes = await getCreatureEffectiveStats.Handle(
            new GetCreatureEffectiveStatsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(attributes.ToResponse());
    }

    private static async Task<Ok<EffectiveAttributesResponse>> GetEquipItemStats(
        Guid creatureId,
        Guid itemId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetEquipItemStatsQuery, Attributes> getEquipItemStats,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );

        var attributes = await getEquipItemStats.Handle(
            new GetEquipItemStatsQuery
            {
                CreatureId = creatureId,
                ItemId = itemId,
                Slot = slot.ToDataModel(),
                BaseAttributes = creature!.BaseAttributes,
                ActiveBuffs = StatFormulas.ToActiveBuffs(creature),
            },
            cancellationToken
        );

        return TypedResults.Ok(attributes.ToResponse());
    }

    private static async Task<Ok<BasicAttackDamageResponse>> GetBasicAttackDamage(
        Guid creatureId,
        [FromServices]
            IQueryHandler<GetCreatureBasicAttackDamageQuery, float> getCreatureBasicAttackDamage,
        CancellationToken cancellationToken
    )
    {
        var damagePerTurn = await getCreatureBasicAttackDamage.Handle(
            new GetCreatureBasicAttackDamageQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new BasicAttackDamageResponse(damagePerTurn));
    }

    private static async Task<Ok<BasicAttackDamageResponse>> GetEquipItemBasicAttackDamage(
        Guid creatureId,
        Guid itemId,
        TRPG.Inventory.Responses.EquipmentSlot slot,
        [FromServices]
            IQueryHandler<GetEquipItemBasicAttackDamageQuery, float> getEquipItemBasicAttackDamage,
        CancellationToken cancellationToken
    )
    {
        var damagePerTurn = await getEquipItemBasicAttackDamage.Handle(
            new GetEquipItemBasicAttackDamageQuery
            {
                CreatureId = creatureId,
                ItemId = itemId,
                Slot = slot.ToDataModel(),
            },
            cancellationToken
        );

        return TypedResults.Ok(new BasicAttackDamageResponse(damagePerTurn));
    }

    private static async Task<Ok<SkillProgressSummary[]>> GetSkills(
        Guid creatureId,
        [FromServices]
            IQueryHandler<
            GetCreatureSkillsQuery,
            IReadOnlyCollection<CreatureSkillProgress>
        > getCreatureSkills,
        CancellationToken cancellationToken
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(skills.Select(skill => skill.ToSummary()).ToArray());
    }

    private static async Task<Ok<CreatureLevelResponse>> GetLevel(
        Guid creatureId,
        [FromServices] IQueryHandler<GetCreatureLevelQuery, int> getCreatureLevel,
        CancellationToken cancellationToken
    )
    {
        var level = await getCreatureLevel.Handle(
            new GetCreatureLevelQuery { CreatureId = creatureId },
            cancellationToken
        );

        return TypedResults.Ok(new CreatureLevelResponse(level));
    }

    private static async Task<Creature> GetExistingCreature(
        Guid creatureId,
        IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );
        if (creature == null)
        {
            throw new EntityNotFoundException(nameof(Creature), creatureId);
        }

        return creature;
    }

    private static ProblemHttpResult FightHeldProblem(string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Fight active",
            detail: detail
        );

    private static async Task<Results<NoContent, ProblemHttpResult>> BeginCreatureInteraction(
        Guid playerId,
        Guid creatureId,
        Guid worldId,
        [FromServices] IWorldClock worldClock,
        [FromServices] IQueryHandler<HasActiveFightQuery, bool> hasActiveFight,
        [FromServices] ICommandHandler<ReleaseOpenInteractionsCommand> releaseOpenInteractions,
        [FromServices] ICommandHandler<BeginCreatureInteractionCommand> beginInteraction,
        [FromServices] WorldSimulationCoordinator simulation,
        CancellationToken cancellationToken
    )
    {
        if (
            await hasActiveFight.Handle(
                new HasActiveFightQuery { PlayerId = playerId },
                cancellationToken
            )
        )
        {
            return FightHeldProblem("Can't interact with anyone during a fight.");
        }

        await simulation.Request(
            worldId,
            async (_, token) =>
            {
                var gameTime = await worldClock.GetCurrent(worldId, token);
                await releaseOpenInteractions.Handle(
                    new ReleaseOpenInteractionsCommand { WorldId = worldId, GameTime = gameTime },
                    token
                );
                await beginInteraction.Handle(
                    new BeginCreatureInteractionCommand
                    {
                        WorldId = worldId,
                        PlayerId = playerId,
                        CreatureId = creatureId,
                        GameTime = gameTime,
                    },
                    token
                );
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> EndCreatureInteraction(
        Guid playerId,
        Guid creatureId,
        Guid worldId,
        [FromServices] IWorldClock worldClock,
        [FromServices] ICommandHandler<EndCreatureInteractionCommand> endInteraction,
        [FromServices] WorldSimulationCoordinator simulation,
        CancellationToken cancellationToken
    )
    {
        await simulation.Request(
            worldId,
            async (_, token) =>
            {
                var gameTime = await worldClock.GetCurrent(worldId, token);
                await endInteraction.Handle(
                    new EndCreatureInteractionCommand
                    {
                        WorldId = worldId,
                        PlayerId = playerId,
                        CreatureId = creatureId,
                        GameTime = gameTime,
                    },
                    token
                );
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }
}
